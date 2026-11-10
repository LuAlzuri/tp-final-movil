using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Turnero.App.Seguridad;

public sealed class ApiGestion(SesionService sesion)
{
    internal async Task<List<T>> ListarPaginadoAsync<T>(string ruta, CancellationToken ct, string? recurso = null)
    {
        const int tamanoPagina = 100;
        var registros = new List<T>();
        for (var pagina = 1; ; pagina++)
        {
            using var respuesta = await EnviarAsync(HttpMethod.Get, $"{ruta}?pagina={pagina}&tamano={tamanoPagina}",
                ct, recurso: recurso);
            var resultados = await LeerAsync<List<T>>(respuesta, ct);
            registros.AddRange(resultados);
            if (resultados.Count < tamanoPagina)
                return registros;
        }
    }

    internal async Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string ruta, CancellationToken ct,
        HttpContent? contenido = null, string? recurso = null)
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
                    HttpStatusCode.Forbidden => recurso switch
                    {
                        "veterinarios" => "Tu cuenta no tiene permiso para gestionar veterinarios.",
                        "auditoria" => "Tu cuenta no tiene permiso para consultar auditorías.",
                        "turnos" => "Tu cuenta no tiene permiso para consultar turnos.",
                        "paciente" => "Tu cuenta no tiene permiso para consultar al paciente.",
                        "tutores" or "telefonos" => "Tu cuenta no tiene permiso para gestionar tutores.",
                        _ => "Tu cuenta no tiene permiso para gestionar catálogos."
                    },
                    HttpStatusCode.NotFound => recurso switch
                    {
                        "veterinarios" => "El veterinario ya no está disponible.",
                        "auditoria" => "El informe ya no está disponible.",
                        "turnos" => "El turno ya no está disponible.",
                        "paciente" => "La ficha del paciente ya no está disponible.",
                        "tutores" => "El tutor ya no está disponible.",
                        "telefonos" => "El teléfono o el tutor ya no está disponible.",
                        _ => "La entrada ya no está disponible."
                    },
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

                throw new GestionApiException(mensaje);
            }
        }
        catch (HttpRequestException ex)
        {
            throw new GestionApiException(ex.StatusCode == HttpStatusCode.Unauthorized
                ? "Tu sesión venció. Volvé a iniciar sesión."
                : "No se pudo conectar con la API. Revisá tu conexión y volvé a intentar.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new GestionApiException("La API tardó demasiado en responder. Revisá el listado antes de repetir la operación.");
        }
    }

    internal static async Task<T> LeerAsync<T>(HttpResponseMessage respuesta, CancellationToken ct)
    {
        try
        {
            return await respuesta.Content.ReadFromJsonAsync<T>(ct)
                ?? throw new GestionApiException("La API devolvió una respuesta vacía.");
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new GestionApiException("La API devolvió una respuesta inesperada.");
        }
    }

    private sealed record ErrorApi(string? Error);
}

public sealed class GestionApiException(string mensaje) : Exception(mensaje);
