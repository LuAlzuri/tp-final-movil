using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Turnero.App.Seguridad;

namespace Turnero.App.Veterinarios;

public sealed class VeterinariosApi(SesionService sesion)
{
    private const string Ruta = "api/veterinarios";
    private const int TamanoPagina = 100;

    public async Task<List<Veterinario>> ListarAsync(CancellationToken ct = default)
    {
        var veterinarios = new List<Veterinario>();
        for (var pagina = 1; ; pagina++)
        {
            using var respuesta = await EnviarAsync(HttpMethod.Get, $"{Ruta}?pagina={pagina}&tamano={TamanoPagina}", ct);
            var resultados = await LeerAsync<List<Veterinario>>(respuesta, ct);
            veterinarios.AddRange(resultados);
            if (resultados.Count < TamanoPagina)
                return veterinarios;
        }
    }

    public async Task<Veterinario> ObtenerAsync(int id, CancellationToken ct = default)
    {
        using var respuesta = await EnviarAsync(HttpMethod.Get, $"{Ruta}/{id}", ct);
        return await LeerAsync<Veterinario>(respuesta, ct);
    }

    public async Task CrearAsync(DatosAltaVeterinario datos, CancellationToken ct = default)
    {
        using var contenido = JsonContent.Create(new
        {
            datos.Nombre, datos.Apellido, datos.Username, datos.Email, datos.Matricula, datos.Contrasena
        });
        using var respuesta = await EnviarAsync(HttpMethod.Post, Ruta, ct, contenido);
    }

    public async Task ModificarAsync(int id, DatosVeterinario datos, CancellationToken ct = default)
    {
        using var contenido = JsonContent.Create(new
        {
            datos.Nombre, datos.Apellido, datos.Username, datos.Email, datos.Matricula
        });
        using var respuesta = await EnviarAsync(HttpMethod.Put, $"{Ruta}/{id}", ct, contenido);
    }

    public async Task EliminarAsync(int id, CancellationToken ct = default)
    {
        using var respuesta = await EnviarAsync(HttpMethod.Delete, $"{Ruta}/{id}", ct);
    }

    private async Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string ruta, CancellationToken ct,
        HttpContent? contenido = null)
    {
        using var solicitud = new HttpRequestMessage(metodo, ruta) { Content = contenido };
        try
        {
            var respuesta = await sesion.EnviarAutenticadoAsync(solicitud, ct);
            if (respuesta.IsSuccessStatusCode)
                return respuesta;

            using (respuesta)
            {
                var mensaje = respuesta.StatusCode switch
                {
                    HttpStatusCode.Unauthorized => "Tu sesión venció. Volvé a iniciar sesión.",
                    HttpStatusCode.Forbidden => "Tu cuenta no tiene permiso para gestionar veterinarios.",
                    HttpStatusCode.NotFound => "El veterinario ya no está disponible.",
                    HttpStatusCode.BadRequest => "Revisá los datos ingresados.",
                    HttpStatusCode.Conflict => "La operación entra en conflicto con los datos registrados.",
                    _ => "La API no pudo completar la operación."
                };

                if (respuesta.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
                {
                    try
                    {
                        var detalle = await respuesta.Content.ReadFromJsonAsync<ErrorApi>(ct);
                        if (!string.IsNullOrWhiteSpace(detalle?.Error))
                            mensaje = detalle.Error;
                    }
                    catch (Exception ex) when (ex is JsonException or NotSupportedException)
                    {
                        // Algunos errores de validación no incluyen el campo error.
                    }
                }

                throw new VeterinariosException(mensaje);
            }
        }
        catch (HttpRequestException ex)
        {
            throw new VeterinariosException(ex.StatusCode == HttpStatusCode.Unauthorized
                ? "Tu sesión venció. Volvé a iniciar sesión."
                : "No se pudo conectar con la API. Revisá tu conexión y volvé a intentar.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new VeterinariosException("La API tardó demasiado en responder. Revisá el listado antes de repetir la operación.");
        }
    }

    private static async Task<T> LeerAsync<T>(HttpResponseMessage respuesta, CancellationToken ct)
    {
        try
        {
            return await respuesta.Content.ReadFromJsonAsync<T>(ct)
                ?? throw new VeterinariosException("La API devolvió una respuesta vacía.");
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new VeterinariosException("La API devolvió una respuesta inesperada.");
        }
    }

    private sealed record ErrorApi(string? Error);
}

public sealed class VeterinariosException(string mensaje) : Exception(mensaje);
