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
    private static async Task VerificarPerrosAsync(FabricaApi fabrica, HttpClient cliente, string tokenAdmin)
    {
        const string ruta = "/api/perros";
        await using var alcance = fabrica.Services.CreateAsyncScope();
        var db = alcance.ServiceProvider.GetRequiredService<AppDbContext>();
        var patologia = new Patologia { Descripcion = "Patología de prueba" };
        var otroTutor = new Tutor { Usuario = NuevaCuenta("tutor-otro", Usuario.RolTutor) };
        db.AddRange(patologia, otroTutor);
        await db.SaveChangesAsync();
        var tutorEliminadoId = await db.Tutores.Where(t => t.Eliminado).Select(t => t.Id).FirstAsync();

        Autorizar(cliente, null);
        var vet = await IniciarSesionAsync(cliente, "vet-nuevo@turnero.test");
        var tutor = await IniciarSesionAsync(cliente, "tutor-servicios@turnero.test");
        var otro = await IniciarSesionAsync(cliente, "tutor-otro@turnero.test");
        var hoy = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow,
            "America/Argentina/Buenos_Aires"));
        var datos = new CrearPerroSolicitud
        {
            TutorId = tutor.Usuario.TutorId!.Value, Nombre = "Mora", FechaNacimiento = hoy.AddYears(-2),
            GrupoEtarioId = 2, TamanioId = 2
        };

        Autorizar(cliente, vet.Token);
        await EsperarCodigo(cliente.PostAsJsonAsync(ruta, datos with { TutorId = tutorEliminadoId }),
            HttpStatusCode.NotFound, "El alta requiere un tutor activo");
        await EsperarCodigo(cliente.PostAsJsonAsync(ruta, datos with { FechaNacimiento = hoy.AddDays(1) }),
            HttpStatusCode.BadRequest, "El nacimiento no puede ser futuro");
        await EsperarCodigo(cliente.PostAsJsonAsync(ruta, datos with { TamanioId = int.MaxValue }),
            HttpStatusCode.BadRequest, "El tamaño debe existir");
        using var alta = await cliente.PostAsJsonAsync(ruta, datos);
        Verificar(alta.StatusCode == HttpStatusCode.Created && alta.Headers.Location is not null, "Alta de perro");
        var perro = (await alta.Content.ReadFromJsonAsync<PerroRespuesta>())!;
        var rutaPerro = $"{ruta}/{perro.Id}";
        var rutaPatologias = rutaPerro + "/patologias";
        var rutaPatologia = $"{rutaPatologias}/{patologia.Id}";
        var asignacion = new AsignarPatologiaSolicitud { PatologiaId = patologia.Id, Observacion = "Nota inicial" };
        await EsperarCodigo(cliente.PutAsJsonAsync(rutaPerro, datos with { Nombre = "Mora actualizada", Reactivo = true }),
            HttpStatusCode.NoContent, "Edición de perro");
        await EsperarCodigo(cliente.PostAsJsonAsync(rutaPatologias, asignacion), HttpStatusCode.Created, "Asignación de patología");
        await EsperarCodigo(cliente.PostAsJsonAsync(rutaPatologias, asignacion), HttpStatusCode.Conflict, "Asignación duplicada");
        await EsperarCodigo(cliente.PutAsJsonAsync(rutaPatologia, new DatosPerroPatologiaSolicitud { Controlado = true }),
            HttpStatusCode.NoContent, "Actualización de patología");
        await EsperarCodigo(cliente.DeleteAsync(rutaPatologia), HttpStatusCode.NoContent, "Retiro de patología");
        await EsperarCodigo(cliente.PostAsJsonAsync(rutaPatologias, asignacion with { Controlado = true }),
            HttpStatusCode.Created, "La patología retirada se puede volver a asignar");

        Autorizar(cliente, otro.Token);
        await EsperarCodigo(cliente.GetAsync(rutaPerro), HttpStatusCode.NotFound, "Un tutor no puede consultar perros ajenos");
        var ajenos = await cliente.GetFromJsonAsync<List<PerroRespuesta>>($"{ruta}?tutorId={perro.TutorId}");
        Verificar(ajenos?.Count == 0, "El filtro no permite listar perros ajenos");
        Autorizar(cliente, tutor.Token);
        var propio = await cliente.GetFromJsonAsync<PerroRespuesta>(rutaPerro);
        Verificar(propio is { Nombre: "Mora actualizada", Reactivo: true, Patologias: [{ Controlado: true }] },
            "El tutor puede ver su perro y la patología asignada");
        await EsperarCodigo(cliente.DeleteAsync(rutaPatologia), HttpStatusCode.Forbidden, "El tutor no puede quitar patologías");

        var reserva = new CupoTurno
        {
            CupoDiario = new CupoDiario { FechaCupo = hoy.AddDays(1), NroCupoDis = 1 },
            Turno = new Turno { PerroId = perro.Id }
        };
        db.CuposTurnos.Add(reserva);
        await db.SaveChangesAsync();
        Autorizar(cliente, vet.Token);
        await EsperarCodigo(cliente.DeleteAsync(rutaPerro), HttpStatusCode.Conflict, "Un turno pendiente impide la baja");
        reserva.Turno.EstadoId = Estado.Rechazado;
        await db.SaveChangesAsync();
        await EsperarCodigo(cliente.DeleteAsync(rutaPerro), HttpStatusCode.NoContent, "Baja lógica de perro");
        await EsperarCodigo(cliente.GetAsync(rutaPerro), HttpStatusCode.NotFound, "El perro dado de baja sale de las consultas operativas");
        Verificar(await db.Perros.AnyAsync(p => p.Id == perro.Id && p.Eliminado)
            && await db.Turnos.AnyAsync(t => t.Id == reserva.TurnoId && t.PerroId == perro.Id)
            && await db.PerrosPatologias.AnyAsync(r => r.PerroId == perro.Id && r.PatologiaId == patologia.Id),
            "La baja conserva el historial y las patologías");

        Autorizar(cliente, tokenAdmin);
        var movimientos = await cliente.GetFromJsonAsync<List<MovimientoAuditoriaRespuesta>>("/api/auditoria/movimientos?entidad=PerroPatologia");
        Verificar(movimientos?.Count == 4
            && movimientos.All(m => m.EntidadId == $"{perro.Id}:{patologia.Id}" && m.UsuarioId == vet.Usuario.Id),
            "La auditoría identifica la asignación exacta y a su autor");
    }
}
