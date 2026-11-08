using System.Text.Json.Serialization;

namespace turnero_peluqueria.Entities;

public sealed class Usuario
{
    public const string RolAdministrador = "Administrador";
    public const string RolVeterinario = "Veterinario";
    public const string RolTutor = "Tutor";

    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    [JsonIgnore]
    public string PasswordHash { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string Rol { get; set; } = RolTutor;
}
