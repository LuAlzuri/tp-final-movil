using System.ComponentModel.DataAnnotations;

namespace turnero_peluqueria.DTOs;

public sealed record CrearVeterinarioSolicitud : AltaCuentaSolicitud
{
    [Range(1, 99999)]
    public int Matricula { get; init; }
}

public sealed record ModificarVeterinarioSolicitud : DatosCuentaSolicitud
{
    [Range(1, 99999)]
    public int Matricula { get; init; }
}

public sealed record VeterinarioRespuesta(int Id, int UsuarioId, string Username, string Email, string Nombre, string Apellido, int Matricula);
