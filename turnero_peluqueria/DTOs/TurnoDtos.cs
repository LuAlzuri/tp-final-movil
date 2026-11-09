using System.ComponentModel.DataAnnotations;

namespace turnero_peluqueria.DTOs;

public record ServiciosTurnoSolicitud
{
    public const int MaximoServicios = 10;

    [Required]
    [MinLength(1, ErrorMessage = "Elegí al menos un servicio.")]
    [MaxLength(MaximoServicios, ErrorMessage = "Podés elegir hasta 10 servicios por turno.")]
    public List<int> ServicioIds { get; init; } = [];
}

public sealed record SolicitarTurnoSolicitud : ServiciosTurnoSolicitud
{
    [Range(1, int.MaxValue, ErrorMessage = "Indicá un perro válido.")]
    public int PerroId { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "Indicá un cupo diario válido.")]
    public int CupoId { get; init; }

}

public sealed record AprobarTurnoSolicitud
{
    [Range(1, int.MaxValue, ErrorMessage = "La cantidad de cupos debe ser positiva.")]
    public int? Cantidad { get; init; }

    [StringLength(500, ErrorMessage = "La observación admite hasta 500 caracteres.")]
    public string? ObservacionCupo { get; init; }
}

public sealed record TratamientoRealizadoSolicitud
{
    [Range(1, int.MaxValue, ErrorMessage = "Indicá un tratamiento válido.")]
    public int TratamientoId { get; init; }

    [StringLength(500, ErrorMessage = "La razón admite hasta 500 caracteres.")]
    public string? Razon { get; init; }
}

public sealed record CompletarTurnoSolicitud
{
    [Required, MaxLength(ServiciosTurnoSolicitud.MaximoServicios)]
    public required List<int> ServicioIdsCompletados { get; init; }

    [Required]
    public List<TratamientoRealizadoSolicitud?> Tratamientos { get; init; } = [];

    [StringLength(1000, ErrorMessage = "Las observaciones admiten hasta 1000 caracteres.")]
    public string? Observaciones { get; init; }
}

public sealed record ServicioTurnoRespuesta(int ServicioId, string Descripcion, bool Completado);

public sealed record TratamientoTurnoRespuesta(int TratamientoId, string Descripcion, string? Razon);

public sealed record TurnoRespuesta(int Id, int PerroId, string PerroNombre, int CupoId, DateOnly FechaCupo,
    int CuposReservados, string? ObservacionCupo, int EstadoId, string EstadoDescripcion, int? VeterinarioId, string? Observaciones,
    DateTime FechaSolicitud, DateTime FechaEstado, List<ServicioTurnoRespuesta> Servicios,
    List<TratamientoTurnoRespuesta> Tratamientos);
