using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using turnero_peluqueria.DTOs;

namespace turnero_peluqueria.Verificaciones;

internal static partial class VerificacionAutenticacion
{
    private static async Task VerificarConsultaDeErroresAsync(FabricaApi fabrica, HttpClient cliente, string tokenAdmin)
    {
        const string ruta = "/api/auditoria/errores";
        Autorizar(cliente, null);
        await EsperarCodigo(cliente.GetAsync(ruta), HttpStatusCode.Unauthorized, "Los errores requieren una sesión");
        var vet = await IniciarSesionAsync(cliente, "vet-nuevo@turnero.test");
        Autorizar(cliente, vet.Token);
        await EsperarCodigo(cliente.GetAsync(ruta), HttpStatusCode.Forbidden, "Los errores son exclusivos del administrador");

        var carpeta = Path.GetDirectoryName(fabrica.RutaErrores)!;
        var prefijo = Path.GetFileNameWithoutExtension(fabrica.RutaErrores);
        var archivoInicial = Path.Combine(carpeta, prefijo + "20260310.log");
        var archivoRotado = Path.Combine(carpeta, prefijo + "20260310_001.log");
        var archivoAjeno = Path.Combine(carpeta, prefijo + "ajeno.log");
        string Entrada(string id, string timestamp, string nivel = "Error") => JsonSerializer.Serialize(new
        {
            Timestamp = timestamp, Level = nivel, MessageTemplate = "Error de prueba {IdSolicitud}",
            RenderedMessage = "detalle-sensible-de-prueba",
            Exception = "traza-sensible-de-prueba",
            Properties = new { IdSolicitud = id, SourceContext = "Verificacion", Metodo = "GET", Ruta = "/api/perros" }
        });

        try
        {
            await File.WriteAllLinesAsync(archivoInicial, new[]
            {
                Entrada("anterior", "2026-03-10T02:59:00+00:00"),
                Entrada("primero", "2026-03-10T03:00:00+00:00"),
                Entrada("advertencia", "2026-03-10T10:00:00+00:00", "Warning"),
                "{registro incompleto"
            });
            await File.WriteAllLinesAsync(archivoRotado, new[]
            {
                Entrada("segundo", "2026-03-11T02:59:00+00:00", "Fatal"),
                Entrada("siguiente", "2026-03-11T03:00:00+00:00")
            });
            await File.WriteAllLinesAsync(archivoAjeno, new[] { Entrada("ajeno", "2026-03-10T12:00:00+00:00") });
            fabrica.Services.GetRequiredService<ILoggerFactory>().CreateLogger("VerificacionErrores")
                .LogError("Error de verificación {IdSolicitud}", "error-del-logger");

            Autorizar(cliente, tokenAdmin);
            using var respuesta = await cliente.GetAsync(ruta + "?desde=2026-03-10&hasta=2026-03-10&tamano=1");
            Verificar(respuesta.StatusCode == HttpStatusCode.OK && respuesta.Headers.CacheControl?.NoStore == true,
                "La respuesta de errores no debe cachearse");
            var json = await respuesta.Content.ReadAsStringAsync();
            Verificar(!json.Contains("sensible-de-prueba", StringComparison.Ordinal), "La respuesta omite el mensaje expandido y la traza");
            var paginaUno = await respuesta.Content.ReadFromJsonAsync<List<ErrorLogRespuesta>>();
            Verificar(paginaUno is [{ IdSolicitud: "segundo", Nivel: "Fatal", Ruta: "/api/perros" }]
                && paginaUno[0].FechaUtc.Offset == TimeSpan.Zero, "Primera página por fecha y archivos rotados");
            var paginaDos = await cliente.GetFromJsonAsync<List<ErrorLogRespuesta>>(
                ruta + "?desde=2026-03-10&hasta=2026-03-10&pagina=2&tamano=1");
            Verificar(paginaDos is [{ IdSolicitud: "primero", Nivel: "Error" }], "Segunda página con el primer error del día argentino");
            var paginaTres = await cliente.GetFromJsonAsync<List<ErrorLogRespuesta>>(
                ruta + "?desde=2026-03-10&hasta=2026-03-10&pagina=3&tamano=1");
            Verificar(paginaTres is [], "Se excluyen otras fechas, advertencias, líneas inválidas y archivos ajenos");
            var registrados = await cliente.GetFromJsonAsync<List<ErrorLogRespuesta>>(ruta);
            Verificar(registrados?.Any(e => e.IdSolicitud == "error-del-logger" && e.Origen == "VerificacionErrores") == true,
                "El endpoint lee los errores que escribe la configuración activa de Serilog");
            await EsperarCodigo(cliente.GetAsync(ruta + "?desde=2026-03-11&hasta=2026-03-10"),
                HttpStatusCode.BadRequest, "Se rechazan períodos invertidos");
        }
        finally
        {
            File.Delete(archivoInicial);
            File.Delete(archivoRotado);
            File.Delete(archivoAjeno);
        }
    }
}
