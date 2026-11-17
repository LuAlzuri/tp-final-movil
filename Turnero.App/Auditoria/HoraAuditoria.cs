using System.Globalization;

namespace Turnero.App.Auditoria;

internal static class HoraAuditoria
{
    private static readonly TimeZoneInfo ZonaArgentina =
        TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");

    public static string Formatear(DateTimeOffset fecha) =>
        TimeZoneInfo.ConvertTime(fecha, ZonaArgentina)
            .ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.GetCultureInfo("es-AR"));
}
