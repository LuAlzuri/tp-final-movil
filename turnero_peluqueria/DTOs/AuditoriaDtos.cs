namespace turnero_peluqueria.DTOs;

public sealed record MovimientoAuditoriaRespuesta(long Id, int? UsuarioId, string? UsuarioEmail, string Accion,
    string Entidad, string? EntidadId, DateTime FechaUtc, string? Ip, string? Observacion);
