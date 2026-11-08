namespace turnero_peluqueria.Entities;

public sealed class PerroPatologia
{
    public int PerroId { get; set; }
    public int PatologiaId { get; set; }
    public bool Controlado { get; set; }
    public string? Observacion { get; set; }
    public Perro Perro { get; set; } = null!;
    public Patologia Patologia { get; set; } = null!;
}
