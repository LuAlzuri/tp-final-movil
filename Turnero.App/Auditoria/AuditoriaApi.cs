using Turnero.App.Seguridad;

namespace Turnero.App.Auditoria;

internal sealed record ErrorAuditoria(DateTimeOffset FechaUtc, string Nivel, string Mensaje,
    string? Origen, string? Metodo, string? Ruta, string? IdSolicitud);

internal sealed record MovimientoAuditoria(long Id, int? UsuarioId, string? UsuarioEmail, string Accion,
    string Entidad, string? EntidadId, DateTime FechaUtc, string? Ip, string? Observacion);

internal sealed class AuditoriaApi(ApiGestion api)
{
    public const int TamanoPagina = 20;

    public Task<List<ErrorAuditoria>> ListarErroresAsync(int pagina, CancellationToken ct) =>
        ListarAsync<ErrorAuditoria>("errores", pagina, ct);

    public Task<List<MovimientoAuditoria>> ListarMovimientosAsync(int pagina, CancellationToken ct) =>
        ListarAsync<MovimientoAuditoria>("movimientos", pagina, ct);

    private async Task<List<T>> ListarAsync<T>(string tipo, int pagina, CancellationToken ct)
    {
        var ruta = $"api/auditoria/{tipo}?pagina={pagina}&tamano={TamanoPagina}";
        using var respuesta = await api.EnviarAsync(HttpMethod.Get, ruta, ct, recurso: "auditoria");
        return await ApiGestion.LeerAsync<List<T>>(respuesta, ct);
    }
}
