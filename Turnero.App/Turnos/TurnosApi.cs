using System.Net.Http.Json;
using Turnero.App.Seguridad;

namespace Turnero.App.Turnos;

public enum EstadoTurno
{
    Pendiente = 1,
    Aprobado = 2,
    Rechazado = 3,
    CanceladoPorTutor = 4,
    CanceladoPorVeterinario = 5,
    Completado = 6
}

internal sealed record Turno(int Id, int PerroId, string PerroNombre, EstadoTurno EstadoId, string EstadoDescripcion, DateOnly FechaCupo,
    int CuposReservados, string? ObservacionCupo, string? Observaciones,
    List<ServicioTurno> Servicios, List<TratamientoTurno> Tratamientos);

internal sealed record ServicioTurno(int ServicioId, string Descripcion, bool Completado);
internal sealed record TratamientoTurno(string Descripcion, string? Razon);
internal sealed record PacienteTurno(DateOnly FechaNacimiento, bool Reactivo, bool CalVacCom, List<PatologiaPaciente> Patologias);
internal sealed record PatologiaPaciente(string Descripcion, bool Controlado, string? Observacion);

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

    public async Task<PacienteTurno> ObtenerPacienteAsync(int perroId, CancellationToken ct)
    {
        using var respuesta = await api.EnviarAsync(HttpMethod.Get, $"api/perros/{perroId}", ct, recurso: "paciente");
        return await ApiGestion.LeerAsync<PacienteTurno>(respuesta, ct);
    }

    public async Task CambiarEstadoAsync(Turno turno, EstadoTurno destino,
        IReadOnlyCollection<int> serviciosCompletados, string? observaciones, CancellationToken ct)
    {
        var accion = destino switch
        {
            EstadoTurno.Aprobado => "aprobar",
            EstadoTurno.Rechazado => "rechazar",
            EstadoTurno.CanceladoPorVeterinario => "cancelar",
            EstadoTurno.Completado => "completar",
            _ => throw new ArgumentOutOfRangeException(nameof(destino))
        };
        object? datos = destino switch
        {
            EstadoTurno.Aprobado => new { turno.ObservacionCupo },
            EstadoTurno.Completado => new { ServicioIdsCompletados = serviciosCompletados, Observaciones = observaciones },
            _ => null
        };
        using var contenido = datos is null ? null : JsonContent.Create(datos);
        using var respuesta = await api.EnviarAsync(HttpMethod.Post, $"{Ruta}/{turno.Id}/{accion}",
            ct, contenido, recurso: "turnos");
    }
}
