using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using turnero_peluqueria.Data;
using turnero_peluqueria.DTOs;
using turnero_peluqueria.Entities;
using turnero_peluqueria.Exceptions;

namespace turnero_peluqueria.Services;

public sealed class ReportesService(AppDbContext db)
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-AR");
    private const string Azul = "#173E52";
    private const string Texto = "#233746";
    private const string Secundario = "#486272";
    private const string Fondo = "#E9F3F5";
    private const string Borde = "#D7E2E8";

    public async Task<byte[]> FrecuenciaSolicitudesAsync(PeriodoReporteSolicitud solicitud, CancellationToken ct)
    {
        var periodo = Periodo.Crear(solicitud);
        var porDia = new SortedDictionary<DateOnly, int>();
        var fechas = db.Turnos.Where(t => t.FechaSolicitud >= periodo.InicioUtc && t.FechaSolicitud < periodo.FinUtc)
            .Select(t => t.FechaSolicitud);
        await foreach (var fecha in fechas.AsAsyncEnumerable().WithCancellation(ct))
        {
            var dia = FechaArgentina.DesdeUtc(fecha);
            porDia[dia] = porDia.GetValueOrDefault(dia) + 1;
        }

        return Generar("Frecuencia de solicitudes", periodo,
            "Solicitudes registradas por día según la fecha de creación, cualquiera sea el estado posterior.", contenido =>
            {
                contenido.Item().Element(c => Total(c, "Solicitudes en el período", porDia.Values.Sum()));
                contenido.Item().Element(c => Tabla(c, "Fecha de solicitud", "Solicitudes",
                    porDia.Select(fila => (Fecha(fila.Key), Numero(fila.Value))), "No hubo solicitudes en el período."));
            });
    }

    public async Task<byte[]> ServiciosCompletosAsync(PeriodoReporteSolicitud solicitud, CancellationToken ct)
    {
        var periodo = Periodo.Crear(solicitud);
        var cantidad = await TurnosCompletados(periodo).CountAsync(t =>
            db.ServiciosPorTurno.Any(s => s.TurnoId == t.Id)
            && !db.ServiciosPorTurno.Any(s => s.TurnoId == t.Id && !s.Completado), ct);

        return Generar("Turnos con servicio completo", periodo,
            "Se cuentan turnos finalizados en el período con al menos un servicio registrado y todos sus servicios realizados.",
            contenido => contenido.Item().Element(c => Total(c, "Turnos con todos los servicios realizados", cantidad)));
    }

    public async Task<byte[]> ServiciosIncompletosAsync(PeriodoReporteSolicitud solicitud, CancellationToken ct)
    {
        var periodo = Periodo.Crear(solicitud);
        var turnos = TurnosCompletados(periodo)
            .Where(t => db.ServiciosPorTurno.Any(s => s.TurnoId == t.Id && !s.Completado));

        await using var transaccion = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var cantidad = await turnos.CountAsync(ct);
        var patologias = await (
            from turno in turnos
            join relacion in db.PerrosPatologias on turno.PerroId equals relacion.PerroId
            group relacion by new { relacion.PatologiaId, relacion.Patologia.Descripcion } into grupo
            orderby grupo.Count() descending, grupo.Key.Descripcion, grupo.Key.PatologiaId
            select new { grupo.Key.Descripcion, Cantidad = grupo.Count() }).ToListAsync(ct);

        var observaciones = await turnos.Where(t => t.Observaciones != null && t.Observaciones != "")
            .OrderBy(t => t.FechaEstado).ThenBy(t => t.Id)
            .Select(t => new { t.Id, t.FechaEstado, t.Observaciones }).ToListAsync(ct);
        await transaccion.CommitAsync(ct);

        return Generar("Turnos con servicios incompletos", periodo,
            "Se incluyen turnos finalizados en el período con al menos un servicio sin realizar.", contenido =>
            {
                contenido.Item().Element(c => Total(c, "Turnos con servicios incompletos", cantidad));
                contenido.Item().EnsureSpace(85).Column(seccion =>
                {
                    seccion.Spacing(6);
                    seccion.Item().Text("Patologías asociadas").FontSize(14).SemiBold().FontColor(Azul);
                    seccion.Item().Text("Se usan las patologías actuales de cada perro. Cada patología se cuenta una vez por turno.")
                        .FontColor(Secundario);
                    seccion.Item().Element(c => Tabla(c, "Patología", "Turnos",
                        patologias.Select(p => (p.Descripcion, Numero(p.Cantidad))), "No hay patologías asociadas."));
                });
                contenido.Item().EnsureSpace(85).Column(seccion =>
                {
                    seccion.Spacing(6);
                    seccion.Item().Text("Observaciones de los turnos").FontSize(14).SemiBold().FontColor(Azul);
                    seccion.Item().Element(c => Tabla(c, "Turno y fecha de finalización", "Observación",
                        observaciones.Select(t => ($"#{t.Id} | {Fecha(FechaArgentina.DesdeUtc(t.FechaEstado))}", t.Observaciones!)),
                        "No hay observaciones registradas.", segundaColumnaAncha: true));
                });
            });
    }

    private IQueryable<Turno> TurnosCompletados(Periodo periodo) =>
        db.Turnos.AsNoTracking().Where(t => t.EstadoId == Estado.Completado
            && t.FechaEstado >= periodo.InicioUtc && t.FechaEstado < periodo.FinUtc);

    private static byte[] Generar(string titulo, Periodo periodo, string criterio, Action<ColumnDescriptor> componer) =>
        Document.Create(documento => documento.Page(pagina =>
        {
            pagina.Size(PageSizes.A4);
            pagina.Margin(38);
            pagina.DefaultTextStyle(estilo => estilo.FontSize(10).FontColor(Texto));

            pagina.Header().BorderBottom(1).BorderColor(Borde).PaddingBottom(12).Column(cabecera =>
            {
                cabecera.Spacing(4);
                cabecera.Item().Text("TURNERO DE PELUQUERÍA CANINA").FontSize(9).SemiBold().FontColor(Secundario);
                cabecera.Item().Text(titulo).FontSize(23).SemiBold().FontColor(Azul);
                cabecera.Item().Text($"Período: {Fecha(periodo.Desde)} al {Fecha(periodo.Hasta)} · Hora argentina")
                    .FontColor(Secundario);
            });

            pagina.Content().PaddingVertical(16).Column(contenido =>
            {
                contenido.Spacing(14);
                contenido.Item().Text(criterio).FontColor(Secundario);
                componer(contenido);
            });

            pagina.Footer().BorderTop(1).BorderColor(Borde).PaddingTop(8).Row(pie =>
            {
                pie.RelativeItem().Text("Informe de gestión").FontSize(9).FontColor(Secundario);
                pie.AutoItem().Text(texto =>
                {
                    texto.DefaultTextStyle(estilo => estilo.FontSize(9).FontColor(Secundario));
                    texto.Span("Página ");
                    texto.CurrentPageNumber();
                    texto.Span(" de ");
                    texto.TotalPages();
                });
            });
        })).GeneratePdf();

    private static void Total(IContainer contenedor, string etiqueta, int cantidad) =>
        contenedor.Background(Fondo).Padding(12).Row(fila =>
        {
            fila.RelativeItem().AlignMiddle().Text(etiqueta).SemiBold().FontColor(Azul);
            fila.AutoItem().Text(Numero(cantidad)).FontSize(22).SemiBold().FontColor(Azul);
        });

    private static void Tabla(IContainer contenedor, string primera, string segunda,
        IEnumerable<(string Etiqueta, string Valor)> filas, string sinDatos, bool segundaColumnaAncha = false)
    {
        var datos = filas.ToList();
        if (datos.Count == 0)
        {
            contenedor.Background(Fondo).Padding(10).Text(sinDatos).FontColor(Secundario);
            return;
        }

        contenedor.Table(tabla =>
        {
            tabla.ColumnsDefinition(columnas =>
            {
                if (segundaColumnaAncha)
                {
                    columnas.ConstantColumn(130);
                    columnas.RelativeColumn();
                }
                else
                {
                    columnas.RelativeColumn();
                    columnas.ConstantColumn(100);
                }
            });

            tabla.Header(cabecera =>
            {
                cabecera.Cell().Background(Azul).Padding(8).Text(primera).SemiBold().FontColor(Colors.White);
                var celda = cabecera.Cell().Background(Azul).Padding(8);
                if (!segundaColumnaAncha)
                    celda = celda.AlignRight();
                celda.Text(segunda).SemiBold().FontColor(Colors.White);
            });

            foreach (var (etiqueta, valor) in datos)
            {
                tabla.Cell().Element(Celda).Text(etiqueta);
                var celda = tabla.Cell().Element(Celda);
                if (!segundaColumnaAncha)
                    celda = celda.AlignRight();
                celda.Text(valor);
            }
        });
    }

    private static IContainer Celda(IContainer c) => c.BorderBottom(1).BorderColor(Borde).Padding(8);

    private static string Fecha(DateOnly dia) => dia.ToString("dd/MM/yyyy", Cultura);

    private static string Numero(int cantidad) => cantidad.ToString("N0", Cultura);

    private sealed record Periodo(DateOnly Desde, DateOnly Hasta, DateTime InicioUtc, DateTime FinUtc)
    {
        public static Periodo Crear(PeriodoReporteSolicitud datos)
        {
            if (datos.Desde is not DateOnly desde || datos.Hasta is not DateOnly hasta || desde > hasta)
                throw new ApiException("Indicá un período con fecha inicial menor o igual a la final.");
            if (desde < new DateOnly(1753, 1, 1) || hasta == DateOnly.MaxValue)
                throw new ApiException("El período contiene una fecha fuera del rango admitido.");

            return new Periodo(desde, hasta, FechaArgentina.InicioDiaUtc(desde), FechaArgentina.InicioDiaUtc(hasta.AddDays(1)));
        }
    }
}
