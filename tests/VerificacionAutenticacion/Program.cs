using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using turnero_peluqueria.Data;
using turnero_peluqueria.DTOs;
using turnero_peluqueria.Entities;
using turnero_peluqueria.Exceptions;
using turnero_peluqueria.Services;

namespace turnero_peluqueria.Verificaciones;

internal static partial class VerificacionAutenticacion
{
    private const string Ruta = "/api/autenticacion/";
    private const string ContrasenaInicial = "Prueba-inicial-001";
    private const string ContrasenaNueva = "Prueba-nueva-002";
    private static int verificaciones;

    public static async Task Main()
    {
        await using var fabrica = new FabricaApi();
        using var cliente = fabrica.CrearCliente();
        var (veterinarioId, tutorId) = await PrepararCuentasAsync(fabrica);

        await EsperarCodigo(cliente.GetAsync(Ruta + "mi-perfil"), HttpStatusCode.Unauthorized, "Un visitante no puede leer el perfil");
        await EsperarCodigo(cliente.PostAsJsonAsync(Ruta + "cambiar-contrasena", Cambio(ContrasenaInicial, ContrasenaNueva)),
            HttpStatusCode.Unauthorized, "Un visitante no puede cambiar la contraseña");

        var admin = await IniciarSesionAsync(cliente, "ADMIN@TURNERO.TEST");
        Verificar(admin.Usuario.Rol == Usuario.RolAdministrador && admin.Usuario.TutorId is null && admin.Usuario.VeterinarioId is null,
            "El administrador no debe tener subperfil");
        var vet = await IniciarSesionAsync(cliente, "vet@turnero.test");
        Verificar(vet.Usuario.Rol == Usuario.RolVeterinario && vet.Usuario.VeterinarioId == veterinarioId
            && vet.Usuario.Id != veterinarioId && vet.Usuario.TutorId is null, "El perfil veterinario debe resolverse por UserId");

        var tutor = await IniciarSesionAsync(cliente, "tutor@turnero.test");
        Verificar(tutor.Usuario.Rol == Usuario.RolTutor && tutor.Usuario.TutorId == tutorId
            && tutor.Usuario.Id != tutorId && tutor.Usuario.VeterinarioId is null, "El perfil tutor debe resolverse por UserId");

        var token = new JwtSecurityTokenHandler().ReadJwtToken(tutor.Token);
        Verificar(token.Subject == tutor.Usuario.Id.ToString() && token.Claims.Single(c => c.Type == "role").Value == Usuario.RolTutor,
            "El JWT debe identificar al usuario y su rol");
        Verificar(token.ValidTo == tutor.ExpiraUtc && token.ValidTo > DateTime.UtcNow,
            "La fecha de expiración de la respuesta debe coincidir con la del token");
        Verificar(!token.Claims.Any(c => c.Type.Contains("password", StringComparison.OrdinalIgnoreCase)
            || c.Value == ContrasenaInicial), "El token no debe contener credenciales");

        var invalidas = await LeerErrorLoginAsync(cliente, "tutor@turnero.test", "Contrasena-incorrecta");
        var inexistente = await LeerErrorLoginAsync(cliente, "ausente@turnero.test", ContrasenaInicial);
        var perfilInvalido = await LeerErrorLoginAsync(cliente, "sin-perfil@turnero.test", ContrasenaInicial);
        Verificar(invalidas == inexistente && inexistente == perfilInvalido,
            "El inicio de sesión no debe distinguir cuenta inexistente, perfil ausente y contraseña incorrecta");

        Autorizar(cliente, vet.Token);
        await EsperarCodigo(cliente.GetAsync(Ruta + "mi-perfil"), HttpStatusCode.OK, "El JWT del veterinario debe funcionar");
        Autorizar(cliente, admin.Token);
        await EsperarCodigo(cliente.GetAsync(Ruta + "mi-perfil"), HttpStatusCode.OK, "El JWT del administrador debe funcionar");
        Autorizar(cliente, tutor.Token);
        using (var respuesta = await cliente.GetAsync(Ruta + "mi-perfil"))
        {
            Verificar(respuesta.StatusCode == HttpStatusCode.OK, "El JWT del tutor debe funcionar");
            var perfil = await respuesta.Content.ReadFromJsonAsync<UsuarioRespuesta>();
            Verificar(perfil == tutor.Usuario, "El perfil debe corresponder al sujeto del token");
        }

        foreach (var (alterado, motivo) in new[]
        {
            (RecrearToken(token, fabrica.Clave, rol: Usuario.RolAdministrador), "rol alterado"),
            (RecrearToken(token, fabrica.Clave, audiencia: "otra-app"), "audiencia inválida"),
            (RecrearToken(token, fabrica.Clave, emisor: "otro-emisor"), "emisor inválido"),
            (RecrearToken(token, new string('X', 64)), "firma inválida"),
            (RecrearToken(token, fabrica.Clave, vencido: true), "token vencido")
        })
        {
            Autorizar(cliente, alterado);
            await EsperarCodigo(cliente.GetAsync(Ruta + "mi-perfil"), HttpStatusCode.Unauthorized, $"Debe rechazarse {motivo}");
        }

        Autorizar(cliente, tutor.Token);
        await EsperarCodigo(cliente.PostAsJsonAsync(Ruta + "cambiar-contrasena", Cambio("incorrecta", ContrasenaNueva)),
            HttpStatusCode.Forbidden, "El cambio debe exigir la contraseña actual");
        await EsperarCodigo(cliente.PostAsJsonAsync(Ruta + "cambiar-contrasena", Cambio(ContrasenaInicial, "corta")),
            HttpStatusCode.BadRequest, "Debe validarse la longitud de la contraseña nueva");
        await EsperarCodigo(cliente.PostAsJsonAsync(Ruta + "cambiar-contrasena", Cambio(ContrasenaInicial, ContrasenaInicial)),
            HttpStatusCode.BadRequest, "La contraseña nueva debe ser distinta");

        Autorizar(cliente, null);
        var segundaSesion = await IniciarSesionAsync(cliente, "tutor@turnero.test");
        Autorizar(cliente, tutor.Token);
        await EsperarCodigo(cliente.PostAsJsonAsync(Ruta + "cambiar-contrasena", Cambio(ContrasenaInicial, ContrasenaNueva)),
            HttpStatusCode.NoContent, "El tutor debe poder cambiar su contraseña");
        await EsperarCodigo(cliente.GetAsync(Ruta + "mi-perfil"), HttpStatusCode.Unauthorized, "El cambio debe invalidar el token utilizado");
        Autorizar(cliente, segundaSesion.Token);
        await EsperarCodigo(cliente.GetAsync(Ruta + "mi-perfil"), HttpStatusCode.Unauthorized, "El cambio debe invalidar otras sesiones");

        Autorizar(cliente, null);
        await LeerErrorLoginAsync(cliente, "tutor@turnero.test", ContrasenaInicial);
        var nuevaSesion = await IniciarSesionAsync(cliente, "tutor@turnero.test", ContrasenaNueva);
        Autorizar(cliente, nuevaSesion.Token);
        await EsperarCodigo(cliente.GetAsync(Ruta + "mi-perfil"), HttpStatusCode.OK, "El token nuevo debe funcionar");

        await using (var alcance = fabrica.Services.CreateAsyncScope())
        {
            var db = alcance.ServiceProvider.GetRequiredService<AppDbContext>();
            var auditoria = await db.AuditoriaAcciones.SingleAsync(a => a.Accion == "CambiarContrasena");
            Verificar(auditoria.UsuarioId == tutor.Usuario.Id && auditoria.EntidadId == tutor.Usuario.Id.ToString()
                && auditoria.Observacion is null, "El cambio debe quedar auditado sin credenciales");
            var usuario = await db.Usuarios.SingleAsync(u => u.Id == tutor.Usuario.Id);
            Verificar(usuario.PasswordHash != ContrasenaNueva, "No debe persistirse la contraseña en texto plano");

            var perfil = await db.Tutores.SingleAsync(t => t.Id == tutorId);
            perfil.Eliminado = true;
            await db.SaveChangesAsync();
        }

        await EsperarCodigo(cliente.GetAsync(Ruta + "mi-perfil"), HttpStatusCode.Unauthorized, "Una baja lógica debe invalidar los tokens existentes");
        Autorizar(cliente, null);
        Verificar(await LeerErrorLoginAsync(cliente, "tutor@turnero.test", ContrasenaNueva) == invalidas,
            "Un perfil eliminado no debe iniciar sesión ni revelar su estado");

        using var swagger = JsonDocument.Parse(await cliente.GetStringAsync("/swagger/v1/swagger.json"));
        var rutas = swagger.RootElement.GetProperty("paths");
        Verificar(rutas.EnumerateObject().Count(ruta => ruta.Name.StartsWith(Ruta, StringComparison.Ordinal)) == 3
            && rutas.TryGetProperty(Ruta + "iniciar-sesion", out var loginSwagger)
            && loginSwagger.GetProperty("post").GetProperty("security").GetArrayLength() == 0,
            "Swagger debe mostrar solo las tres rutas y permitir el inicio de sesión anónimo");
        Verificar(swagger.RootElement.GetProperty("components").GetProperty("securitySchemes")
            .GetProperty("Bearer").GetProperty("scheme").GetString() == "bearer", "Swagger debe ofrecer autenticación Bearer");

        await VerificarLimiteAsync();
        await VerificarVeterinariosAsync(cliente, admin.Token, vet.Token);
        await VerificarTutoresAsync(cliente, admin.Token, vet.Token);
        await VerificarBajasYAuditoriaAsync(fabrica, cliente, admin, vet);
        await VerificarCatalogosDePerrosAsync(cliente, admin.Token);
        await VerificarCatalogosDeTurnosAsync(fabrica, cliente, admin.Token);
        Console.WriteLine($"API verificada: {verificaciones} comprobaciones con una base temporal en memoria.");
    }

    private static async Task<(int VeterinarioId, int TutorId)> PrepararCuentasAsync(FabricaApi fabrica)
    {
        await using var alcance = fabrica.Services.CreateAsyncScope();
        var db = alcance.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        var servicio = alcance.ServiceProvider.GetRequiredService<AutenticacionService>();
        var solicitud = new CrearAdministradorSolicitud
        {
            Username = "admin", Email = "admin@turnero.test",
            Nombre = "Admin", Apellido = "Prueba", Contrasena = ContrasenaInicial
        };

        await servicio.CrearAdministradorInicialAsync(solicitud, CancellationToken.None);
        try
        {
            await servicio.CrearAdministradorInicialAsync(solicitud, CancellationToken.None);
            throw new InvalidOperationException("El arranque no debe permitir un segundo administrador.");
        }
        catch (ApiException ex)
        {
            Verificar(ex.CodigoEstado == 409, "Un segundo administrador debe rechazarse por conflicto");
        }

        Verificar(await db.Usuarios.CountAsync(u => u.Rol == Usuario.RolAdministrador) == 1,
            "Debe existir un único administrador inicial");
        Verificar(await db.AuditoriaAcciones.CountAsync(a => a.Accion == "CrearAdministradorInicial") == 1,
            "El alta inicial debe quedar auditada");

        var vet = new Veterinario { Id = 150, Usuario = NuevaCuenta("vet", Usuario.RolVeterinario, hashAnterior: true), Matricula = 123 };
        var tutor = new Tutor { Id = 250, Usuario = NuevaCuenta("tutor", Usuario.RolTutor) };
        db.AddRange(vet, tutor, NuevaCuenta("sin-perfil", Usuario.RolTutor));
        await db.SaveChangesAsync();
        return (vet.Id, tutor.Id);
    }

    private static Usuario NuevaCuenta(string username, string rol, bool hashAnterior = false)
    {
        var usuario = new Usuario { Username = username, Nombre = username, Apellido = "Prueba", Email = $"{username}@turnero.test", Rol = rol };
        var configuracion = new PasswordHasherOptions
        {
            CompatibilityMode = hashAnterior ? PasswordHasherCompatibilityMode.IdentityV2 : PasswordHasherCompatibilityMode.IdentityV3
        };
        usuario.PasswordHash = new PasswordHasher<Usuario>(Options.Create(configuracion)).HashPassword(usuario, ContrasenaInicial);
        return usuario;
    }

    private static async Task<SesionRespuesta> IniciarSesionAsync(HttpClient cliente, string correo, string contrasena = ContrasenaInicial)
    {
        using var respuesta = await cliente.PostAsJsonAsync(Ruta + "iniciar-sesion", new IniciarSesionSolicitud { Email = correo, Contrasena = contrasena });
        var contenido = await respuesta.Content.ReadAsStringAsync();
        Verificar(respuesta.StatusCode == HttpStatusCode.OK, $"El inicio de sesión válido debe responder 200 (respondió {(int)respuesta.StatusCode}: {contenido})");
        Verificar(!contenido.Contains("passwordHash", StringComparison.OrdinalIgnoreCase) && !contenido.Contains(contrasena),
            "La respuesta no debe exponer credenciales");
        return (await respuesta.Content.ReadFromJsonAsync<SesionRespuesta>())!;
    }

    private static async Task<string> LeerErrorLoginAsync(HttpClient cliente, string correo, string contrasena)
    {
        using var respuesta = await cliente.PostAsJsonAsync(Ruta + "iniciar-sesion", new IniciarSesionSolicitud { Email = correo, Contrasena = contrasena });
        Verificar(respuesta.StatusCode == HttpStatusCode.Unauthorized, "Las credenciales o perfiles inválidos deben devolver 401");
        return await respuesta.Content.ReadAsStringAsync();
    }

    private static async Task VerificarLimiteAsync()
    {
        await using var fabrica = new FabricaApi(limite: 2);
        using var cliente = fabrica.CrearCliente();
        for (var intento = 1; intento <= 3; intento++)
        {
            using var solicitud = new HttpRequestMessage(HttpMethod.Post, Ruta + "iniciar-sesion")
            {
                Content = JsonContent.Create(new IniciarSesionSolicitud())
            };
            solicitud.Headers.Add("X-Forwarded-For", $"192.0.2.{intento}");
            using var respuesta = await cliente.SendAsync(solicitud);
            Verificar(respuesta.StatusCode == (intento == 3 ? HttpStatusCode.TooManyRequests : HttpStatusCode.BadRequest),
                "El límite de inicio de sesión debe aplicarse sin confiar en X-Forwarded-For");
            if (intento == 3)
                Verificar(respuesta.Headers.RetryAfter?.Delta > TimeSpan.Zero, "El límite debe informar Retry-After");
        }

        await EsperarCodigo(cliente.GetAsync(Ruta + "mi-perfil"), HttpStatusCode.Unauthorized, "El límite solo debe aplicarse al inicio de sesión");
    }

    private static string RecrearToken(JwtSecurityToken original, string clave, string? rol = null, string? audiencia = null,
        string? emisor = null, bool vencido = false)
    {
        var claims = original.Claims.Where(c => c.Type is not ("nbf" or "exp" or "iat" or "iss" or "aud"))
            .Select(c => c.Type == "role" && rol is not null ? new Claim("role", rol) : c);
        var ahora = DateTime.UtcNow;
        var token = new JwtSecurityToken(emisor ?? FabricaApi.Emisor, audiencia ?? FabricaApi.Audiencia, claims,
            ahora.AddMinutes(-10), ahora.AddMinutes(vencido ? -2 : 10),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(clave)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static CambiarContrasenaSolicitud Cambio(string actual, string nueva) =>
        new() { ContrasenaActual = actual, ContrasenaNueva = nueva };

    private static void Autorizar(HttpClient cliente, string? token) =>
        cliente.DefaultRequestHeaders.Authorization = token is null ? null : new AuthenticationHeaderValue("Bearer", token);

    private static async Task EsperarCodigo(Task<HttpResponseMessage> solicitud, HttpStatusCode esperado, string mensaje)
    {
        using var respuesta = await solicitud;
        Verificar(respuesta.StatusCode == esperado, $"{mensaje}: se esperaba {(int)esperado}, se recibió {(int)respuesta.StatusCode}");
    }

    private static void Verificar(bool condicion, string mensaje)
    {
        if (!condicion)
            throw new InvalidOperationException(mensaje);
        verificaciones++;
    }
}
