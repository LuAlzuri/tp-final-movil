using System.ComponentModel.DataAnnotations;

namespace turnero_peluqueria.DTOs;

public sealed record CrearTutorSolicitud : AltaCuentaSolicitud
{
    [Required, MinLength(1)]
    public List<TelefonoTutorSolicitud?> Telefonos { get; init; } = [];
}

public sealed record TelefonoTutorSolicitud
{
    [Required, StringLength(100)]
    public string NombreProp { get; init; } = string.Empty;

    [Required, StringLength(20)]
    public string NroTel { get; init; } = string.Empty;
}

public sealed record TelefonoTutorRespuesta(int Id, string NombreProp, string NroTel);

public sealed record TutorRespuesta(int Id, int UsuarioId, string Username, string Email, string Nombre, string Apellido,
    List<TelefonoTutorRespuesta> Telefonos);
