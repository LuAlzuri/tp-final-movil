using System.ComponentModel.DataAnnotations;

namespace turnero_peluqueria.DTOs;

public sealed record PeriodoReporteSolicitud
{
    [Required(ErrorMessage = "Indicá la fecha inicial.")]
    public DateOnly? Desde { get; init; }

    [Required(ErrorMessage = "Indicá la fecha final.")]
    public DateOnly? Hasta { get; init; }
}
