using Turnero.App.Seguridad;

namespace Turnero.App.Turnos;

internal sealed record Turno(int Id, string PerroNombre, int EstadoId, string EstadoDescripcion, DateOnly FechaCupo,
    int CuposReservados, string? ObservacionCupo, string? Observaciones,
    List<ServicioTurno> Servicios, List<TratamientoTurno> Tratamientos);

internal sealed record ServicioTurno(string Descripcion, bool Completado);
internal sealed record TratamientoTurno(string Descripcion, string? Razon);

internal sealed class TurnosApi(ApiGestion api)
{
    private const string Ruta = "api/turnos";
    public const int TamanoPagina = 20;
    public const int MaximaPagina = 1_000_000;

    public async Task<List<Turno>> ListarAsync(int pagina, CancellationToken ct)
    {
        using var respuesta = await api.EnviarAsync(HttpMethod.Get,
            $"{Ruta}?pagina={pagina}&tamano={TamanoPagina}", ct, recurso: "turnos");
        return await ApiGestion.LeerAsync<List<Turno>>(respuesta, ct);
    }

    public async Task<Turno> ObtenerAsync(int id, CancellationToken ct)
    {
        using var respuesta = await api.EnviarAsync(HttpMethod.Get, $"{Ruta}/{id}", ct, recurso: "turnos");
        return await ApiGestion.LeerAsync<Turno>(respuesta, ct);
    }
}
