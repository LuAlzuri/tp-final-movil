namespace turnero_peluqueria.Entities;

public sealed class Tutor
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public bool Eliminado { get; set; }
    public Usuario Usuario { get; set; } = null!;
}
