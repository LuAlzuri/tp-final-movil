namespace turnero_peluqueria.Entities;

public sealed class TelefonoTutor
{
    public int Id { get; set; }
    public int TutorId { get; set; }
    public string NombreProp { get; set; } = string.Empty;
    public string NroTel { get; set; } = string.Empty;
    public Tutor Tutor { get; set; } = null!;
}
