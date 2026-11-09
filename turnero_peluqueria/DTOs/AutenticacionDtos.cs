using System.ComponentModel.DataAnnotations;

namespace turnero_peluqueria.DTOs;

public sealed record IniciarSesionSolicitud
{
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo no es válido.")]
    [StringLength(254, ErrorMessage = "El correo admite hasta 254 caracteres.")]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [StringLength(128, ErrorMessage = "La contraseña admite hasta 128 caracteres.")]
    public string Contrasena { get; init; } = string.Empty;
}

public sealed record CambiarContrasenaSolicitud
{
    [Required(ErrorMessage = "La contraseña actual es obligatoria.")]
    [StringLength(128, ErrorMessage = "La contraseña actual admite hasta 128 caracteres.")]
    public string ContrasenaActual { get; init; } = string.Empty;

    [Required(ErrorMessage = "La contraseña nueva es obligatoria.")]
    [StringLength(128, MinimumLength = 12, ErrorMessage = "La contraseña nueva debe tener entre 12 y 128 caracteres.")]
    public string ContrasenaNueva { get; init; } = string.Empty;
}

public sealed record CrearAdministradorSolicitud
{
    [Required, StringLength(100)]
    public string Username { get; init; } = string.Empty;

    [Required, StringLength(100)]
    public string Nombre { get; init; } = string.Empty;

    [Required, StringLength(100)]
    public string Apellido { get; init; } = string.Empty;

    [Required, EmailAddress, StringLength(254)]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 12)]
    public string Contrasena { get; init; } = string.Empty;
}

public sealed record UsuarioRespuesta(
    int Id, string Username, string Email, string Nombre, string Apellido, string Rol,
    int? VeterinarioId, int? TutorId);

public sealed record SesionRespuesta(string Token, DateTime ExpiraUtc, UsuarioRespuesta Usuario);
