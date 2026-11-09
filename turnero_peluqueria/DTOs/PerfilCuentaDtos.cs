using System.ComponentModel.DataAnnotations;

namespace turnero_peluqueria.DTOs;

public record DatosCuentaSolicitud
{
    [Required, StringLength(100)]
    public string Username { get; init; } = string.Empty;

    [Required, EmailAddress, StringLength(254)]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(100)]
    public string Nombre { get; init; } = string.Empty;

    [Required, StringLength(100)]
    public string Apellido { get; init; } = string.Empty;
}

public record AltaCuentaSolicitud : DatosCuentaSolicitud
{
    [Required, StringLength(128, MinimumLength = 12)]
    public string Contrasena { get; init; } = string.Empty;
}
