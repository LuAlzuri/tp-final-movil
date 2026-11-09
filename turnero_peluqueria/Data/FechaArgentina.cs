namespace turnero_peluqueria.Data;

internal static class FechaArgentina
{
    private static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");

    public static DateOnly Hoy => DesdeUtc(DateTime.UtcNow);

    public static DateOnly DesdeUtc(DateTime fecha) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(fecha, Zona));

    public static DateTime InicioDiaUtc(DateOnly dia)
    {
        var inicio = dia.ToDateTime(TimeOnly.MinValue);
        while (Zona.IsInvalidTime(inicio))
            inicio = inicio.AddMinutes(1);
        if (Zona.IsAmbiguousTime(inicio))
            return new DateTimeOffset(inicio, Zona.GetAmbiguousTimeOffsets(inicio).Max()).UtcDateTime;

        return TimeZoneInfo.ConvertTimeToUtc(inicio, Zona);
    }
}
