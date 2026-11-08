namespace turnero_peluqueria.Entities;

public sealed class AuditoriaAccion
{
    public long Id { get; set; }
    // Nulo para acciones del sistema, como el veto automatico.
    public int? UsuarioId { get; set; }
    public string? UsuarioEmail { get; set; }
    public string Accion { get; set; } = string.Empty;
    public string Entidad { get; set; } = string.Empty;
    public string? EntidadId { get; set; }
    public DateTime FechaUtc { get; set; } = DateTime.UtcNow;
    public string? Ip { get; set; }
    public string? Observacion { get; set; }
    public Usuario? Usuario { get; set; }
}
