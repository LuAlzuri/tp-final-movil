using System.ComponentModel.DataAnnotations;

namespace turnero_peluqueria.DTOs;

public sealed record SolicitarTurnoSolicitud
{
    [Range(1, int.MaxValue, ErrorMessage = "Indicá un perro válido.")]
    public int PerroId { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "Indicá un cupo diario válido.")]
    public int CupoId { get; init; }

    [Required]
    [MinLength(1, ErrorMessage = "Elegí al menos un servicio.")]
    [MaxLength(10, ErrorMessage = "Podés elegir hasta 10 servicios por turno.")]
    public List<int> ServicioIds { get; init; } = [];
}

public sealed record ServicioTurnoRespuesta(int ServicioId, string Descripcion, bool Completado);

public sealed record TratamientoTurnoRespuesta(int TratamientoId, string Descripcion, string? Razon);

public sealed record TurnoRespuesta(int Id, int PerroId, string PerroNombre, int CupoId, DateOnly FechaCupo,
    int CuposReservados, int EstadoId, string EstadoDescripcion, int? VeterinarioId, string? Observaciones,
    DateTime FechaSolicitud, DateTime FechaEstado, List<ServicioTurnoRespuesta> Servicios,
    List<TratamientoTurnoRespuesta> Tratamientos);
