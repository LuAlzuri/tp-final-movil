namespace turnero_peluqueria.Entities;

public sealed class Veto
{
    public const string MotivoAutomatico = "Superó número de cancelaciones";

    public int Id { get; set; }
    public int TutorId { get; set; }
    public int? VeterinarioId { get; set; }
    public DateTime FechaInicio { get; set; } = DateTime.UtcNow;
    public string? Motivo { get; set; }
    public DateTime? FechaFin { get; set; }
    public string? MotivoFinVeto { get; set; }
    public Tutor Tutor { get; set; } = null!;
    public Veterinario? Veterinario { get; set; }
}
