namespace turnero_peluqueria.Entities;

public sealed class Estado
{
    public const int Pendiente = 1;
    public const int Aprobado = 2;
    public const int Rechazado = 3;
    public const int CanceladoPorTutor = 4;
    public const int CanceladoPorVeterinario = 5;
    public const int Completado = 6;

    public int Id { get; set; }
    public string Descripcion { get; set; } = string.Empty;
}
