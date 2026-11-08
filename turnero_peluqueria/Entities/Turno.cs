namespace turnero_peluqueria.Entities;

public sealed class Turno
{
    public int Id { get; set; }
    public int PerroId { get; set; }
    public int EstadoId { get; set; } = Estado.Pendiente;
    public int? VeterinarioId { get; set; }
    public string? Observaciones { get; set; }
    public DateTime FechaSolicitud { get; set; } = DateTime.UtcNow;
    public DateTime FechaEstado { get; set; } = DateTime.UtcNow;
    public Perro Perro { get; set; } = null!;
    public Estado Estado { get; set; } = null!;
    public Veterinario? Veterinario { get; set; }
}
