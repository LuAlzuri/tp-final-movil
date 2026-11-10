using System.ComponentModel.DataAnnotations;

namespace Turnero.App.Veterinarios;

public sealed record Veterinario(int Id, string Username, string Email, string Nombre, string Apellido, int Matricula)
{
    public string NombreCompleto => $"{Nombre} {Apellido}";
}

public class DatosVeterinario
{
    [Required(ErrorMessage = "Ingresá el nombre.")]
    [StringLength(100, ErrorMessage = "El nombre admite hasta 100 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresá el apellido.")]
    [StringLength(100, ErrorMessage = "El apellido admite hasta 100 caracteres.")]
    public string Apellido { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresá el nombre de usuario.")]
    [StringLength(100, ErrorMessage = "El usuario admite hasta 100 caracteres.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresá el correo electrónico.")]
    [EmailAddress(ErrorMessage = "Ingresá un correo electrónico válido.")]
    [StringLength(254, ErrorMessage = "El correo admite hasta 254 caracteres.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresá la matrícula.")]
    [Range(1, 99999, ErrorMessage = "La matrícula debe estar entre 1 y 99999.")]
    public int? Matricula { get; set; }
}

public sealed class DatosAltaVeterinario : DatosVeterinario, IValidatableObject
{
    [Required(ErrorMessage = "Ingresá una contraseña inicial para el veterinario.")]
    [StringLength(128, MinimumLength = 12, ErrorMessage = "La contraseña debe tener entre 12 y 128 caracteres.")]
    public string? Contrasena { get; set; }

    [Required(ErrorMessage = "Repetí la contraseña inicial.")]
    public string? RepetirContrasena { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.Equals(Contrasena, RepetirContrasena, StringComparison.Ordinal))
            yield return new ValidationResult("Las contraseñas no coinciden.", [nameof(RepetirContrasena)]);
    }
}
