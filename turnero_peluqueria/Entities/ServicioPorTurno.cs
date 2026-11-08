namespace turnero_peluqueria.Entities;

public sealed class ServicioPorTurno
{
    public int TurnoId { get; set; }
    public int ServicioId { get; set; }
    public bool Completado { get; set; }
    public Turno Turno { get; set; } = null!;
    public Servicio Servicio { get; set; } = null!;
}
