using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;

namespace Turnero.App.Seguridad;

public sealed class SesionService(HttpClient api, ISecureStorage almacenamiento)
    : AuthenticationStateProvider, IDisposable
{
    private const string ClaveSesion = "turnero.sesion.v1";
    private static readonly AuthenticationState Anonimo = new(new ClaimsPrincipal(new ClaimsIdentity()));

    private readonly SemaphoreSlim acceso = new(1);
    private AuthenticationState estado = Anonimo;
    private SesionGuardada? sesionActual;
    private Timer? vencimiento;
    private bool restaurada;

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        await acceso.WaitAsync();
        try
        {
            if (!restaurada)
            {
                await RestaurarAsync();
                restaurada = true;
            }

            return sesionActual?.ExpiraUtc > DateTimeOffset.UtcNow ? estado : Anonimo;
        }
        finally
        {
            acceso.Release();
        }
    }

    public async Task IniciarSesionAsync(string email, string contrasena, CancellationToken ct = default)
    {
        await acceso.WaitAsync(ct);
        try
        {
            using var respuesta = await api.PostAsJsonAsync(
                "api/autenticacion/iniciar-sesion", new { email = email.Trim(), contrasena }, ct);
            respuesta.EnsureSuccessStatusCode();

            var datos = await respuesta.Content.ReadFromJsonAsync<SesionRespuesta>(ct);
            if (datos is null || string.IsNullOrWhiteSpace(datos.Token) ||
                datos.ExpiraUtc <= DateTimeOffset.UtcNow || !RolHabilitado(datos.Usuario))
            {
                throw new InvalidOperationException("La API devolvió una sesión no válida.");
            }

            var guardada = new SesionGuardada(datos.Token, datos.ExpiraUtc);
            await almacenamiento.SetAsync(ClaveSesion, JsonSerializer.Serialize(guardada));
            Activar(guardada, datos.Usuario!);
            restaurada = true;
            NotifyAuthenticationStateChanged(Task.FromResult(estado));
        }
        finally
        {
            acceso.Release();
        }
    }

    public async Task CerrarSesionAsync()
    {
        await acceso.WaitAsync();
        try
        {
            QuitarSesion();
        }
        finally
        {
            acceso.Release();
        }
    }

    internal async Task<HttpResponseMessage> EnviarAutenticadoAsync(HttpRequestMessage solicitud, CancellationToken ct)
    {
        var baseApi = api.BaseAddress!;
        var destino = new Uri(baseApi, solicitud.RequestUri!);
        if (!string.Equals(destino.GetLeftPart(UriPartial.Authority),
            baseApi.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("La solicitud debe dirigirse a la API de VeteApp.", nameof(solicitud));
        }

        SesionGuardada guardada;
        await acceso.WaitAsync(ct);
        try
        {
            if (sesionActual is not { } actual || actual.ExpiraUtc <= DateTimeOffset.UtcNow)
                throw new HttpRequestException("La sesión venció.", null, HttpStatusCode.Unauthorized);

            guardada = actual;
        }
        finally
        {
            acceso.Release();
        }

        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", guardada.Token);
        var respuesta = await api.SendAsync(solicitud, ct);
        if (respuesta.StatusCode == HttpStatusCode.Unauthorized)
        {
            await acceso.WaitAsync();
            try
            {
                if (ReferenceEquals(sesionActual, guardada))
                    QuitarSesion();
            }
            catch
            {
                respuesta.Dispose();
                throw;
            }
            finally
            {
                acceso.Release();
            }
        }

        return respuesta;
    }

    private async Task RestaurarAsync()
    {
        SesionGuardada? guardada;
        try
        {
            var json = await almacenamiento.GetAsync(ClaveSesion);
            if (json is null)
                return;

            guardada = JsonSerializer.Deserialize<SesionGuardada>(json);
        }
        catch (Exception)
        {
            // El almacenamiento nativo puede quedar ilegible tras restaurar una copia del dispositivo.
            almacenamiento.Remove(ClaveSesion);
            return;
        }

        if (guardada is null || string.IsNullOrWhiteSpace(guardada.Token) ||
            guardada.ExpiraUtc <= DateTimeOffset.UtcNow)
        {
            almacenamiento.Remove(ClaveSesion);
            return;
        }

        try
        {
            using var solicitud = new HttpRequestMessage(HttpMethod.Get, "api/autenticacion/mi-perfil");
            solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", guardada.Token);
            using var respuesta = await api.SendAsync(solicitud);

            if (respuesta.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                almacenamiento.Remove(ClaveSesion);
                return;
            }

            if (!respuesta.IsSuccessStatusCode)
                return;

            var usuario = await respuesta.Content.ReadFromJsonAsync<UsuarioRespuesta>();
            if (RolHabilitado(usuario))
                Activar(guardada, usuario!);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
        {
            // Una falla de comunicación no invalida el token guardado.
        }
    }

    private static bool RolHabilitado(UsuarioRespuesta? usuario) =>
        usuario?.Rol is Roles.Administrador or Roles.Veterinario or Roles.Tutor;

    private void Activar(SesionGuardada guardada, UsuarioRespuesta usuario)
    {
        LimpiarEstado();
        var demora = guardada.ExpiraUtc - DateTimeOffset.UtcNow;
        if (demora <= TimeSpan.Zero)
            return;

        Claim[] datos =
        [
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, usuario.Username),
            new(ClaimTypes.Email, usuario.Email),
            new(ClaimTypes.Role, usuario.Rol)
        ];

        estado = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(datos, "Bearer")));
        sesionActual = guardada;
        vencimiento = new Timer(ignorado => _ = VencerAsync(guardada), null, demora, Timeout.InfiniteTimeSpan);
    }

    private async Task VencerAsync(SesionGuardada guardada)
    {
        await acceso.WaitAsync();
        try
        {
            if (!ReferenceEquals(sesionActual, guardada))
                return;

            LimpiarEstado();
            NotifyAuthenticationStateChanged(Task.FromResult(estado));
        }
        finally
        {
            acceso.Release();
        }
    }

    private void LimpiarEstado()
    {
        vencimiento?.Dispose();
        vencimiento = null;
        sesionActual = null;
        estado = Anonimo;
    }

    private void QuitarSesion()
    {
        almacenamiento.Remove(ClaveSesion);
        LimpiarEstado();
        restaurada = true;
        NotifyAuthenticationStateChanged(Task.FromResult(estado));
    }

    public void Dispose() => vencimiento?.Dispose();

    private sealed record SesionGuardada(string Token, DateTimeOffset ExpiraUtc);
    private sealed record SesionRespuesta(string Token, DateTimeOffset ExpiraUtc, UsuarioRespuesta? Usuario);
    private sealed record UsuarioRespuesta(int Id, string Username, string Email, string Rol);
}
