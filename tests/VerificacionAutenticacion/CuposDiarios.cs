using System.Globalization;
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
    private static async Task VerificarCuposDiariosAsync(FabricaApi fabrica, HttpClient cliente, string tokenAdmin)
    {
        const string ruta = "/api/cupos-diarios";
        Autorizar(cliente, null);
        var vet = await IniciarSesionAsync(cliente, "vet-nuevo@turnero.test");
        var tutor = await IniciarSesionAsync(cliente, "tutor-servicios@turnero.test");
        var hoy = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow,
            "America/Argentina/Buenos_Aires"));
        var datos = new PublicarCupoSolicitud { FechaCupo = hoy.AddDays(4), CuposPublicados = 5 };

        Autorizar(cliente, tutor.Token);
        await EsperarCodigo(cliente.PostAsJsonAsync(ruta, datos), HttpStatusCode.Forbidden, "Solo el veterinario publica cupos");
        Autorizar(cliente, vet.Token);
        await EsperarCodigo(cliente.PostAsJsonAsync(ruta, datos with { FechaCupo = hoy.AddDays(-1) }),
            HttpStatusCode.BadRequest, "No se pueden publicar fechas pasadas");
        await EsperarCodigo(cliente.PostAsJsonAsync(ruta, datos with { CuposPublicados = -1 }),
            HttpStatusCode.BadRequest, "La capacidad no puede ser negativa");
        using var alta = await cliente.PostAsJsonAsync(ruta, datos);
        Verificar(alta.StatusCode == HttpStatusCode.Created && alta.Headers.Location is not null, "Alta de cupo diario");
        var cupo = (await alta.Content.ReadFromJsonAsync<CupoDiarioRespuesta>())!;
        Verificar(cupo.CuposOcupados == 0 && cupo.CuposDisponibles == 5, "El cupo nuevo parte libre");
        await EsperarCodigo(cliente.PostAsJsonAsync(ruta, datos), HttpStatusCode.Conflict, "Fecha de cupo repetida");

        await using var alcance = fabrica.Services.CreateAsyncScope();
        var db = alcance.ServiceProvider.GetRequiredService<AppDbContext>();
        var perroId = await db.Perros.OrderBy(p => p.Id).Select(p => p.Id).FirstAsync();
        var pasado = new CupoDiario { FechaCupo = hoy.AddDays(-1), NroCupoDis = 5 };
        db.Add(pasado);
        foreach (var estadoId in Enumerable.Range(1, 6))
        {
            db.CuposTurnos.Add(new CupoTurno
            {
                CupoId = cupo.Id, Cantidad = estadoId == Estado.Pendiente ? 2 : 1,
                Turno = new Turno { PerroId = perroId, EstadoId = estadoId, VeterinarioId = vet.Usuario.VeterinarioId }
            });
        }
        await db.SaveChangesAsync();

        Autorizar(cliente, tutor.Token);
        var disponible = await cliente.GetFromJsonAsync<CupoDiarioRespuesta>(alta.Headers.Location);
        Verificar(disponible is { CuposOcupados: 4, CuposDisponibles: 1 },
            "Solo ocupan capacidad los turnos pendientes, aprobados y completados");
        var desde = datos.FechaCupo.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var lista = await cliente.GetFromJsonAsync<List<CupoDiarioRespuesta>>($"{ruta}?desde={desde}&hasta={desde}");
        Verificar(lista?.Single().Id == cupo.Id, "La lista permite consultar disponibilidad por fecha");
        await EsperarCodigo(cliente.GetAsync($"{ruta}/{pasado.Id}"), HttpStatusCode.NotFound, "El tutor no ve fechas pasadas");

        Autorizar(cliente, vet.Token);
        await EsperarCodigo(cliente.PutAsJsonAsync(alta.Headers.Location, new CapacidadCupoSolicitud { CuposPublicados = 3 }),
            HttpStatusCode.Conflict, "No se puede bajar la capacidad por debajo de lo ocupado");
        await EsperarCodigo(cliente.PutAsJsonAsync(alta.Headers.Location, new CapacidadCupoSolicitud { CuposPublicados = 4 }),
            HttpStatusCode.NoContent, "La capacidad puede igualar lo ocupado");
        await EsperarCodigo(cliente.DeleteAsync(alta.Headers.Location), HttpStatusCode.Conflict, "Un cupo con turnos no puede borrarse");
        await EsperarCodigo(cliente.DeleteAsync($"{ruta}/{pasado.Id}"), HttpStatusCode.NoContent, "Un cupo libre se puede borrar");

        Autorizar(cliente, tutor.Token);
        await EsperarCodigo(cliente.GetAsync(alta.Headers.Location), HttpStatusCode.NotFound, "El tutor no ve fechas sin disponibilidad");
        lista = await cliente.GetFromJsonAsync<List<CupoDiarioRespuesta>>($"{ruta}?desde={desde}&hasta={desde}");
        Verificar(lista?.Count == 0, "La lista omite fechas sin disponibilidad");

        Autorizar(cliente, tokenAdmin);
        var movimientos = await cliente.GetFromJsonAsync<List<MovimientoAuditoriaRespuesta>>("/api/auditoria/movimientos?entidad=CupoDiario");
        Verificar(movimientos?.Select(a => a.Accion).SequenceEqual(new[] { "Eliminar", "Modificar", "Crear" }) == true,
            "La auditoría incluye las operaciones efectivas");
    }
}
