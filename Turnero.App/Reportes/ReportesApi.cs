using System.Globalization;
using Turnero.App.Seguridad;

namespace Turnero.App.Reportes;

internal sealed record DefinicionInforme(string Tipo, string Titulo, string EtiquetaTotal)
{
    public static IReadOnlyList<DefinicionInforme> Todas { get; } =
    [
        new("frecuencia-solicitudes", "Frecuencia de solicitudes", "Solicitudes en el período"),
        new("servicios-completos", "Turnos con servicio completo", "Turnos con todos los servicios realizados"),
        new("servicios-incompletos", "Turnos con servicios incompletos", "Turnos con servicios incompletos")
    ];
}

internal sealed record SolicitudesPorDia(DateOnly Fecha, int Cantidad);
internal sealed record PatologiaInforme(string Descripcion, int Cantidad);
internal sealed record ObservacionInforme(int TurnoId, DateOnly Fecha, string Observacion);
internal sealed record DatosInforme(int Total, List<SolicitudesPorDia>? Solicitudes,
    List<PatologiaInforme>? Patologias, List<ObservacionInforme>? Observaciones);

internal sealed class ReportesApi(ApiGestion api)
{
    public async Task<DatosInforme> ConsultarAsync(DefinicionInforme informe, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        using var respuesta = await api.EnviarAsync(HttpMethod.Get, Ruta(informe, desde, hasta, "/datos"), ct, recurso: "informes");
        return await ApiGestion.LeerAsync<DatosInforme>(respuesta, ct);
    }

    public async Task<byte[]> GenerarPdfAsync(DefinicionInforme informe, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        using var respuesta = await api.EnviarAsync(HttpMethod.Get, Ruta(informe, desde, hasta, ""), ct, recurso: "informes");
        var pdf = await respuesta.Content.ReadAsByteArrayAsync(ct);
        if (respuesta.Content.Headers.ContentType?.MediaType != "application/pdf" || !pdf.AsSpan().StartsWith("%PDF-"u8))
            throw new GestionApiException("La API devolvió un archivo que no es un PDF válido.");

        return pdf;
    }

    private static string Ruta(DefinicionInforme informe, DateOnly desde, DateOnly hasta, string sufijo) =>
        string.Create(CultureInfo.InvariantCulture, $"api/reportes/{informe.Tipo}{sufijo}?desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}");
}