using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using turnero_peluqueria.Data;
using turnero_peluqueria.DTOs;
using turnero_peluqueria.Entities;

namespace turnero_peluqueria.Verificaciones;

internal static partial class VerificacionAutenticacion
{
    private static async Task VerificarReportesAsync(FabricaApi fabrica, HttpClient cliente, string tokenAdmin)
    {
        const string periodo = "?desde=2026-03-10&hasta=2026-03-10";
        Autorizar(cliente, null);
        var vet = await IniciarSesionAsync(cliente, "vet-nuevo@turnero.test");
        var tutor = await IniciarSesionAsync(cliente, "tutor-otro@turnero.test");
        await using var alcance = fabrica.Services.CreateAsyncScope();
        var db = alcance.ServiceProvider.GetRequiredService<AppDbContext>();
        var perroId = await db.Perros.Where(p => p.TutorId == tutor.Usuario.TutorId).OrderBy(p => p.Id).Select(p => p.Id).FirstAsync();
        var veterinarioId = vet.Usuario.VeterinarioId!.Value;
        var servicios = await db.Servicios.OrderBy(s => s.Id).Select(s => s.Id).Take(2).ToListAsync();
        db.PerrosPatologias.Add(new PerroPatologia
        {
            PerroId = perroId, Patologia = new Patologia { Descripcion = "Dermatitis" }
        });

        var inicio = new DateTime(2026, 3, 10, 3, 0, 0, DateTimeKind.Utc);
        var fin = inicio.AddDays(1);
        void Agregar(DateTime solicitud, DateTime fechaEstado, int estado, string? observacion, params bool[] realizados)
        {
            var turno = new Turno
            {
                PerroId = perroId, EstadoId = estado, VeterinarioId = veterinarioId,
                FechaSolicitud = solicitud, FechaEstado = fechaEstado, Observaciones = observacion
            };
            db.Turnos.Add(turno);
            for (var i = 0; i < realizados.Length; i++)
                db.ServiciosPorTurno.Add(new ServicioPorTurno { Turno = turno, ServicioId = servicios[i], Completado = realizados[i] });
        }

        Agregar(inicio.AddMinutes(-1), inicio.AddHours(10), Estado.Completado, null, true);
        Agregar(inicio, fin.AddMinutes(-1), Estado.Completado, "El perro necesitó pausas durante el baño.", false, false);
        Agregar(inicio.AddHours(1), inicio.AddHours(11), Estado.Completado, "Se completó el corte. El baño quedó pendiente.", true, false);
        Agregar(inicio.AddHours(2), inicio.AddHours(12), Estado.Completado, "Sin servicios asociados");
        Agregar(inicio.AddHours(3), inicio.AddHours(13), Estado.Rechazado, "Observación fuera del informe", false);
        Agregar(fin, fin, Estado.Completado, "Observación del día siguiente", false);
        await db.SaveChangesAsync();

        var rutas = new[] { "frecuencia-solicitudes", "servicios-completos", "servicios-incompletos" };
        foreach (var reporte in rutas)
        {
            var ruta = "/api/reportes/" + reporte;
            Autorizar(cliente, vet.Token);
            await EsperarCodigo(cliente.GetAsync(ruta + periodo), HttpStatusCode.Forbidden, "Solo el administrador descarga reportes");
            await EsperarCodigo(cliente.GetAsync(ruta + "/datos" + periodo), HttpStatusCode.Forbidden, "Solo el administrador consulta reportes");
            Autorizar(cliente, tokenAdmin);
            using var respuesta = await cliente.GetAsync(ruta + periodo);
            Verificar(respuesta.StatusCode == HttpStatusCode.OK && respuesta.Content.Headers.ContentType?.MediaType == "application/pdf"
                && respuesta.Content.Headers.ContentDisposition?.FileName?.Trim('"') == reporte + ".pdf", "La descarga debe devolver un PDF con nombre");
            var bytes = await respuesta.Content.ReadAsByteArrayAsync();
            Verificar(bytes.AsSpan().StartsWith("%PDF-"u8), "El contenido debe ser un PDF generado");
            var carpeta = Environment.GetEnvironmentVariable("TURNERO_PDF_VERIFICACION");
            if (!string.IsNullOrWhiteSpace(carpeta))
                await File.WriteAllBytesAsync(Path.Combine(carpeta, reporte + ".pdf"), bytes);
        }

        var frecuencia = await cliente.GetFromJsonAsync<FrecuenciaSolicitudesDatos>("/api/reportes/frecuencia-solicitudes/datos" + periodo);
        Verificar(frecuencia is { Total: 4 } && frecuencia.Solicitudes.SequenceEqual([new(new DateOnly(2026, 3, 10), 4)]),
            "La frecuencia agrupa las solicitudes por día argentino");
        var completos = await cliente.GetFromJsonAsync<ServiciosCompletosDatos>("/api/reportes/servicios-completos/datos" + periodo);
        Verificar(completos is { Total: 1 }, "Solo se cuentan los turnos finalizados con todos los servicios realizados");
        var incompletos = await cliente.GetFromJsonAsync<ServiciosIncompletosDatos>("/api/reportes/servicios-incompletos/datos" + periodo);
        Verificar(incompletos is { Total: 2 } && incompletos.Patologias.SequenceEqual([new("Dermatitis", 2)])
            && incompletos.Observaciones.Count == 2, "Los datos de servicios incompletos incluyen patologías y observaciones");

        await EsperarCodigo(cliente.GetAsync("/api/reportes/frecuencia-solicitudes?desde=2026-03-11&hasta=2026-03-10"),
            HttpStatusCode.BadRequest, "Se rechaza un período invertido");
        await EsperarCodigo(cliente.GetAsync("/api/reportes/servicios-completos"),
            HttpStatusCode.BadRequest, "Se exigen las dos fechas del período");
        await EsperarCodigo(cliente.GetAsync("/api/reportes/servicios-incompletos?desde=2025-01-01&hasta=2025-01-01"),
            HttpStatusCode.OK, "Un período sin datos también devuelve un PDF");
    }
}
