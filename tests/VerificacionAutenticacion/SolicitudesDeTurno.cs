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
    private static async Task VerificarSolicitudesDeTurnoAsync(FabricaApi fabrica, HttpClient cliente, string tokenAdmin)
    {
        const string ruta = "/api/turnos";
        Autorizar(cliente, null);
        var tutor = await IniciarSesionAsync(cliente, "tutor-servicios@turnero.test");
        var otro = await IniciarSesionAsync(cliente, "tutor-otro@turnero.test");
        var vet = await IniciarSesionAsync(cliente, "vet-nuevo@turnero.test");
        var hoy = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow,
            "America/Argentina/Buenos_Aires"));
        var fecha = hoy.AddDays(7);

        await using var alcance = fabrica.Services.CreateAsyncScope();
        var db = alcance.ServiceProvider.GetRequiredService<AppDbContext>();
        var cupo = new CupoDiario { FechaCupo = fecha, NroCupoDis = 1 };
        var cupoVeto = new CupoDiario { FechaCupo = fecha.AddDays(1), NroCupoDis = 2 };
        var pasado = new CupoDiario { FechaCupo = hoy.AddDays(-1), NroCupoDis = 1 };
        var servicios = Enumerable.Range(1, 11).Select(i => new Servicio { Descripcion = $"Servicio {i}" }).ToList();
        var tutorId = tutor.Usuario.TutorId!.Value;
        var cachorro = new Perro { Nombre = "Cachorro", FechaNacimiento = fecha.AddDays(-44), TutorId = tutorId, GrupoEtarioId = 1, TamanioId = 1 };
        var limite = new Perro { Nombre = "Límite", FechaNacimiento = fecha.AddDays(-45), TutorId = tutorId, GrupoEtarioId = 1, TamanioId = 1 };
        var adulto = new Perro { Nombre = "Adulto", FechaNacimiento = hoy.AddYears(-2), TutorId = tutorId, GrupoEtarioId = 2, TamanioId = 2 };
        db.AddRange(cupo, cupoVeto, pasado, cachorro, limite, adulto);
        db.Servicios.AddRange(servicios);
        await db.SaveChangesAsync();

        var idsServicios = servicios.Select(s => s.Id).Order().ToList();
        var datos = new SolicitarTurnoSolicitud { PerroId = limite.Id, CupoId = cupo.Id, ServicioIds = idsServicios.Take(10).ToList() };
        Autorizar(cliente, otro.Token);
        await EsperarCodigo(cliente.PostAsJsonAsync(ruta, datos), HttpStatusCode.NotFound, "Un tutor no puede pedir turnos para perros ajenos");
        Autorizar(cliente, vet.Token);
        await EsperarCodigo(cliente.PostAsJsonAsync(ruta, datos), HttpStatusCode.Forbidden, "La solicitud por la app corresponde al tutor");
        Autorizar(cliente, tutor.Token);
        await EsperarCodigo(cliente.PostAsJsonAsync(ruta, datos with { PerroId = cachorro.Id }),
            HttpStatusCode.BadRequest, "Un cachorro de 44 días sin vacunación completa no puede reservar");
        await EsperarCodigo(cliente.PostAsJsonAsync(ruta, datos with { CupoId = pasado.Id }),
            HttpStatusCode.BadRequest, "No se admiten fechas pasadas");
        await EsperarCodigo(cliente.PostAsJsonAsync(ruta, datos with { ServicioIds = [] }),
            HttpStatusCode.BadRequest, "Se requiere al menos un servicio");
        await EsperarCodigo(cliente.PostAsJsonAsync(ruta, datos with { ServicioIds = idsServicios }),
            HttpStatusCode.BadRequest, "No se pueden elegir 11 servicios");
        await EsperarCodigo(cliente.PostAsJsonAsync(ruta, datos with { ServicioIds = [idsServicios[0], idsServicios[0]] }),
            HttpStatusCode.BadRequest, "Los servicios deben ser distintos");
        await EsperarCodigo(cliente.PostAsJsonAsync(ruta, datos with { ServicioIds = [int.MaxValue] }),
            HttpStatusCode.BadRequest, "Los servicios deben existir");
        Verificar(!await db.CuposTurnos.AnyAsync(r => r.CupoId == cupo.Id), "Una solicitud inválida no reserva capacidad");

        using var alta = await cliente.PostAsJsonAsync(ruta, datos);
        Verificar(alta.StatusCode == HttpStatusCode.Created && alta.Headers.Location is not null, "A los 45 días puede solicitar el turno");
        var turno = (await alta.Content.ReadFromJsonAsync<TurnoRespuesta>())!;
        Verificar(turno.EstadoId == Estado.Pendiente && turno.CuposReservados == 1 && turno.VeterinarioId is null
            && turno.FechaCupo == fecha && turno.FechaSolicitud == turno.FechaEstado && turno.FechaEstado.Kind == DateTimeKind.Utc
            && turno.Servicios.All(s => !s.Completado) && turno.Servicios.Select(s => s.ServicioId).SequenceEqual(datos.ServicioIds),
            "La respuesta devuelve el turno pendiente, la fecha, la capacidad y los 10 servicios");

        await EsperarCodigo(cliente.PostAsJsonAsync(ruta, datos), HttpStatusCode.Conflict, "Se impide repetir un turno activo del perro");
        await EsperarCodigo(cliente.PostAsJsonAsync(ruta, datos with { PerroId = adulto.Id }),
            HttpStatusCode.Conflict, "Otro perro no puede reservar cuando el cupo está lleno");

        db.Vetos.Add(new Veto { TutorId = tutorId, Motivo = Veto.MotivoAutomatico });
        await db.SaveChangesAsync();
        await EsperarCodigo(cliente.PostAsJsonAsync(ruta, datos with { CupoId = cupoVeto.Id }),
            HttpStatusCode.Forbidden, "Un veto vigente bloquea solicitudes por la app");
        var historial = await cliente.GetFromJsonAsync<List<TurnoRespuesta>>($"{ruta}?perroId={limite.Id}&estadoId=1");
        Verificar(historial?.Single().Id == turno.Id, "El tutor conserva acceso a sus turnos mientras está vetado");

        Autorizar(cliente, otro.Token);
        await EsperarCodigo(cliente.GetAsync(alta.Headers.Location), HttpStatusCode.NotFound, "Un tutor no puede leer turnos ajenos");
        historial = await cliente.GetFromJsonAsync<List<TurnoRespuesta>>($"{ruta}?perroId={limite.Id}");
        Verificar(historial?.Count == 0, "El filtro no expone el historial ajeno");
        Autorizar(cliente, vet.Token);
        await EsperarCodigo(cliente.GetAsync(alta.Headers.Location), HttpStatusCode.OK, "El veterinario puede consultar la solicitud");

        Autorizar(cliente, tokenAdmin);
        var movimientos = await cliente.GetFromJsonAsync<List<MovimientoAuditoriaRespuesta>>("/api/auditoria/movimientos?entidad=Turno");
        Verificar(movimientos is [{ Accion: "Solicitar" }] && movimientos[0].UsuarioId == tutor.Usuario.Id
            && movimientos[0].EntidadId == turno.Id.ToString(), "Solo la solicitud guardada queda auditada");
    }
}
