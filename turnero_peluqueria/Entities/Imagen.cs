namespace turnero_peluqueria.Entities;

public sealed class Imagen
{
    public int Id { get; set; }
    public int PerroId { get; set; }
    public string NombreArchivo { get; set; } = string.Empty;
    public string RutaRelativa { get; set; } = string.Empty;
    public string TipoContenido { get; set; } = string.Empty;
    public long TamanoBytes { get; set; }
    public Perro Perro { get; set; } = null!;
}
