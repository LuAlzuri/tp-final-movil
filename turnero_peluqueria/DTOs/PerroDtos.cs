using System.ComponentModel.DataAnnotations;

namespace turnero_peluqueria.DTOs;

public record DatosPerroSolicitud
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100, ErrorMessage = "El nombre admite hasta 100 caracteres.")]
    public string Nombre { get; init; } = string.Empty;

    public DateOnly FechaNacimiento { get; init; }
    public bool Reactivo { get; init; }
    public bool CalVacCom { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "Indicá un grupo etario válido.")]
    public int GrupoEtarioId { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "Indicá un tamaño válido.")]
    public int TamanioId { get; init; }
}

public sealed record CrearPerroSolicitud : DatosPerroSolicitud
{
    [Range(1, int.MaxValue, ErrorMessage = "Indicá un tutor válido.")]
    public int TutorId { get; init; }
}

public record DatosPerroPatologiaSolicitud
{
    public bool Controlado { get; init; }

    [StringLength(1000, ErrorMessage = "La observación admite hasta 1000 caracteres.")]
    public string? Observacion { get; init; }
}

public sealed record AsignarPatologiaSolicitud : DatosPerroPatologiaSolicitud
{
    [Range(1, int.MaxValue, ErrorMessage = "Indicá una patología válida.")]
    public int PatologiaId { get; init; }
}

public sealed record PerroPatologiaRespuesta(int PatologiaId, string Descripcion, bool Controlado, string? Observacion);

public sealed record PerroRespuesta(int Id, int TutorId, string Nombre, DateOnly FechaNacimiento, bool Reactivo,
    bool CalVacCom, int GrupoEtarioId, int TamanioId, List<PerroPatologiaRespuesta> Patologias, string? ImagenUrl);
