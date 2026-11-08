namespace turnero_peluqueria.Entities;

public sealed class CupoTurno
{
    public int CupoId { get; set; }
    public int TurnoId { get; set; }
    public int Cantidad { get; set; } = 1;
    public string? Observacion { get; set; }
    public CupoDiario CupoDiario { get; set; } = null!;
    public Turno Turno { get; set; } = null!;
}
