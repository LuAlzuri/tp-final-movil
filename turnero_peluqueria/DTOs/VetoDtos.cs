using System.ComponentModel.DataAnnotations;

namespace turnero_peluqueria.DTOs;

public sealed record MotivoVetoSolicitud
{
    [StringLength(500, ErrorMessage = "El motivo admite hasta 500 caracteres.")]
    public string? Motivo { get; init; }
}

public sealed record VetoRespuesta(int Id, int TutorId, int? VeterinarioId, DateTime FechaInicio,
    string? Motivo, DateTime? FechaFin, string? MotivoFinVeto);
