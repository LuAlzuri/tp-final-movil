namespace turnero_peluqueria.Entities;

public sealed class Veterinario
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int Matricula { get; set; }
    public bool Eliminado { get; set; }
    public Usuario Usuario { get; set; } = null!;
}
