using System.ComponentModel.DataAnnotations;

namespace Turnero.App.Tutores;

public sealed record Tutor(int Id, string Username, string Email, string Nombre, string Apellido,
    List<TelefonoTutor> Telefonos)
{
    public string NombreCompleto => $"{Nombre} {Apellido}";
}

public sealed record TelefonoTutor(int Id, string NombreProp, string NroTel);

public sealed class DatosTelefonoTutor
{
    [Required(ErrorMessage = "Ingresá el nombre del contacto.")]
    [StringLength(100, ErrorMessage = "El nombre admite hasta 100 caracteres.")]
    public string NombreProp { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresá el número de teléfono.")]
    [StringLength(20, ErrorMessage = "El número admite hasta 20 caracteres.")]
    public string NroTel { get; set; } = string.Empty;
}

internal class DatosTutor
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
}

internal sealed class DatosAltaTutor : DatosTutor, IValidatableObject
{
    [Required(ErrorMessage = "Ingresá una contraseña inicial para el tutor.")]
    [StringLength(128, MinimumLength = 12, ErrorMessage = "La contraseña debe tener entre 12 y 128 caracteres.")]
    public string? Contrasena { get; set; }

    [Required(ErrorMessage = "Repetí la contraseña inicial.")]
    public string? RepetirContrasena { get; set; }

    [MinLength(1, ErrorMessage = "Agregá al menos un teléfono de contacto.")]
    public List<DatosTelefonoTutor> Telefonos { get; } = [new()];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.Equals(Contrasena, RepetirContrasena, StringComparison.Ordinal))
            yield return new ValidationResult("Las contraseñas no coinciden.", [nameof(RepetirContrasena)]);

        var numeros = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var indice = 0; indice < Telefonos.Count; indice++)
        {
            var telefono = Telefonos[indice];
            var errores = new List<ValidationResult>();
            Validator.TryValidateObject(telefono, new ValidationContext(telefono), errores, validateAllProperties: true);
            foreach (var error in errores)
                yield return new ValidationResult($"Teléfono {indice + 1}: {error.ErrorMessage}");

            if (!string.IsNullOrWhiteSpace(telefono.NroTel) && !numeros.Add(telefono.NroTel.Trim()))
                yield return new ValidationResult($"Teléfono {indice + 1}: el número está repetido.");
        }
    }
}
