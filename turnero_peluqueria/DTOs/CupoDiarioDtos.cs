using System.ComponentModel.DataAnnotations;

namespace turnero_peluqueria.DTOs;

public record CapacidadCupoSolicitud
{
    [Range(0, int.MaxValue, ErrorMessage = "La capacidad no puede ser negativa.")]
    public required int CuposPublicados { get; init; }
}

public sealed record PublicarCupoSolicitud : CapacidadCupoSolicitud
{
    public required DateOnly FechaCupo { get; init; }
}

public sealed record CupoDiarioRespuesta(int Id, DateOnly FechaCupo, int CuposPublicados, int CuposOcupados, int CuposDisponibles);
