namespace turnero_peluqueria.Entities;

public sealed class CupoDiario
{
    public int Id { get; set; }
    public DateOnly FechaCupo { get; set; }
    // Total publicado; la disponibilidad se calcula descontando los cupos ocupados.
    public int NroCupoDis { get; set; }
}
