namespace turnero_peluqueria.Exceptions;

internal sealed class MiddlewareErrores(RequestDelegate siguiente, ILogger<MiddlewareErrores> logger)
{
    public async Task InvokeAsync(HttpContext contexto)
    {
        try
        {
            await siguiente(contexto);
        }
        catch (OperationCanceledException) when (contexto.RequestAborted.IsCancellationRequested)
        {
            // El cliente cerró la solicitud.
        }
        catch (ApiException ex) when (!contexto.Response.HasStarted)
        {
            await ResponderAsync(contexto, ex.CodigoEstado, ex.Message);
        }
        catch (BadHttpRequestException ex) when (!contexto.Response.HasStarted)
        {
            await ResponderAsync(contexto, ex.StatusCode, "La solicitud no es válida.");
        }
        catch (Exception ex) when (!contexto.Response.HasStarted)
        {
            logger.LogError(ex, "Error al procesar {Metodo} {Ruta}. Solicitud {IdSolicitud}",
                contexto.Request.Method, contexto.Request.Path.Value, contexto.TraceIdentifier);
            await ResponderAsync(contexto, StatusCodes.Status500InternalServerError, "Ocurrió un error interno.");
        }
    }

    private static Task ResponderAsync(HttpContext contexto, int codigo, string mensaje)
    {
        contexto.Response.StatusCode = codigo;
        return contexto.Response.WriteAsJsonAsync(new { error = mensaje }, contexto.RequestAborted);
    }
}
