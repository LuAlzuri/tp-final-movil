namespace turnero_peluqueria.DTOs;

public sealed record SolicitudesPorDia(DateOnly Fecha, int Cantidad);
public sealed record FrecuenciaSolicitudesDatos(int Total, IReadOnlyList<SolicitudesPorDia> Solicitudes);
public sealed record ServiciosCompletosDatos(int Total);
public sealed record PatologiaReporte(string Descripcion, int Cantidad);
public sealed record ObservacionReporte(int TurnoId, DateOnly Fecha, string Observacion);
public sealed record ServiciosIncompletosDatos(int Total, IReadOnlyList<PatologiaReporte> Patologias,
    IReadOnlyList<ObservacionReporte> Observaciones);
