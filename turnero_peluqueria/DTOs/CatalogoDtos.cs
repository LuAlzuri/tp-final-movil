using System.ComponentModel.DataAnnotations;

namespace turnero_peluqueria.DTOs;

public sealed record CatalogoSolicitud
{
    [Required(ErrorMessage = "La descripción es obligatoria.")]
    [StringLength(100, ErrorMessage = "La descripción admite hasta 100 caracteres.")]
    public string Descripcion { get; init; } = string.Empty;
}

public sealed record CatalogoRespuesta(int Id, string Descripcion);
