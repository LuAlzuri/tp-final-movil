namespace turnero_peluqueria.Entities;

public sealed class Perro
{
    public int Id { get; set; }
    public int TutorId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public DateOnly FechaNacimiento { get; set; }
    public bool Reactivo { get; set; }
    public bool CalVacCom { get; set; }
    public int GrupoEtarioId { get; set; }
    public int TamanioId { get; set; }
    public bool Eliminado { get; set; }
    public Tutor Tutor { get; set; } = null!;
    public GrupoEtario GrupoEtario { get; set; } = null!;
    public Tamanio Tamanio { get; set; } = null!;
    public Imagen? Imagen { get; set; }
}
