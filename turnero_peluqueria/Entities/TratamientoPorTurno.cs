namespace turnero_peluqueria.Entities;

public sealed class TratamientoPorTurno
{
    public int TurnoId { get; set; }
    public int TratamientoId { get; set; }
    public string? Razon { get; set; }
    public Turno Turno { get; set; } = null!;
    public Tratamiento Tratamiento { get; set; } = null!;
}
