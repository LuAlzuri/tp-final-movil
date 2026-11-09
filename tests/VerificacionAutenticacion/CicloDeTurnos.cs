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
    private static async Task VerificarCicloDeTurnosAsync(FabricaApi fabrica, HttpClient cliente, string tokenAdmin)
    {
        const string ruta = "/api/turnos";
        Autorizar(cliente, null);
        var tutor = await IniciarSesionAsync(cliente, "tutor-otro@turnero.test");
        var otro = await IniciarSesionAsync(cliente, "tutor-servicios@turnero.test");
        var vet = await IniciarSesionAsync(cliente, "vet-nuevo@turnero.test");
        var hoy = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow,
            "America/Argentina/Buenos_Aires"));

        await using var alcance = fabrica.Services.CreateAsyncScope();
        var db = alcance.ServiceProvider.GetRequiredService<AppDbContext>();
        var cupoHoy = await db.CuposDiarios.SingleAsync(c => c.FechaCupo == hoy);
        cupoHoy.NroCupoDis = 3;
        var futuro = new CupoDiario { FechaCupo = hoy.AddDays(14), NroCupoDis = 1 };
        var perro = new Perro
        {
            Nombre = "Milo", FechaNacimiento = hoy.AddYears(-3), TutorId = tutor.Usuario.TutorId!.Value,
            GrupoEtarioId = 2, TamanioId = 2
        };
        var tratamiento = new Tratamiento { Descripcion = "Tratamiento del turno" };
        db.AddRange(futuro, perro, tratamiento);
        await db.SaveChangesAsync();
        var ids = await db.Servicios.OrderBy(s => s.Id).Select(s => s.Id).ToListAsync();
        var datos = new SolicitarTurnoSolicitud { PerroId = perro.Id, CupoId = cupoHoy.Id, ServicioIds = [ids[0]] };

        async Task<TurnoRespuesta> SolicitarAsync(SolicitarTurnoSolicitud solicitud)
        {
            Autorizar(cliente, tutor.Token);
            using var respuesta = await cliente.PostAsJsonAsync(ruta, solicitud);
            Verificar(respuesta.StatusCode == HttpStatusCode.Created, "Se debe poder reservar un cupo liberado");
            return (await respuesta.Content.ReadFromJsonAsync<TurnoRespuesta>())!;
        }

        var turno = await SolicitarAsync(datos);
        var rutaTurno = $"{ruta}/{turno.Id}";
        var servicios = new ServiciosTurnoSolicitud { ServicioIds = [ids[1], ids[2]] };
        Autorizar(cliente, otro.Token);
        await EsperarCodigo(cliente.PutAsJsonAsync(rutaTurno + "/servicios", servicios), HttpStatusCode.NotFound,
            "Un tutor no puede editar servicios de un turno ajeno");
        await EsperarCodigo(cliente.PostAsync(rutaTurno + "/cancelar", null), HttpStatusCode.NotFound,
            "Un tutor no puede cancelar un turno ajeno");
        Autorizar(cliente, tutor.Token);
        await EsperarCodigo(cliente.PutAsJsonAsync(rutaTurno + "/servicios", new ServiciosTurnoSolicitud { ServicioIds = ids }),
            HttpStatusCode.BadRequest, "Editar respeta el límite de 10 servicios");
        await EsperarCodigo(cliente.PutAsJsonAsync(rutaTurno + "/servicios", servicios), HttpStatusCode.NoContent,
            "El tutor puede reemplazar los servicios de un turno pendiente");
        await EsperarCodigo(cliente.PostAsJsonAsync(rutaTurno + "/aprobar", new AprobarTurnoSolicitud()),
            HttpStatusCode.Forbidden, "El tutor no puede aprobar");

        Autorizar(cliente, vet.Token);
        await EsperarCodigo(cliente.PostAsync(rutaTurno + "/cancelar", null), HttpStatusCode.Conflict,
            "El veterinario no puede cancelar un turno pendiente");
        await EsperarCodigo(cliente.PostAsJsonAsync(rutaTurno + "/aprobar", new AprobarTurnoSolicitud { Cantidad = 3 }),
            HttpStatusCode.Conflict, "La ampliación no puede superar la capacidad disponible");
        await EsperarCodigo(cliente.PostAsJsonAsync(rutaTurno + "/aprobar", new AprobarTurnoSolicitud
        {
            Cantidad = 2, ObservacionCupo = " Se necesitan dos cupos "
        }), HttpStatusCode.NoContent, "Se puede aprobar ampliando solo hasta la capacidad libre");
        var aprobado = await cliente.GetFromJsonAsync<TurnoRespuesta>(rutaTurno);
        Verificar(aprobado is { EstadoId: Estado.Aprobado, CuposReservados: 2, ObservacionCupo: "Se necesitan dos cupos" }
            && aprobado.VeterinarioId == vet.Usuario.VeterinarioId
            && aprobado.Servicios.Select(s => s.ServicioId).SequenceEqual(servicios.ServicioIds),
            "La aprobación conserva los servicios y asigna al veterinario");

        Autorizar(cliente, tutor.Token);
        await EsperarCodigo(cliente.PutAsJsonAsync(rutaTurno + "/servicios", servicios), HttpStatusCode.Conflict,
            "Tras aprobar, la lista de servicios solicitados queda fija");
        Autorizar(cliente, vet.Token);
        await EsperarCodigo(cliente.PostAsJsonAsync(rutaTurno + "/completar", new CompletarTurnoSolicitud
        {
            ServicioIdsCompletados = [ids[0]]
        }), HttpStatusCode.BadRequest, "No se puede registrar como realizado un servicio que fue quitado");
        await EsperarCodigo(cliente.PostAsJsonAsync(rutaTurno + "/completar", new CompletarTurnoSolicitud
        {
            ServicioIdsCompletados = [ids[1]],
            Tratamientos = [new TratamientoRealizadoSolicitud { TratamientoId = tratamiento.Id, Razon = " Cuidado posterior " }],
            Observaciones = " Servicio parcial "
        }), HttpStatusCode.NoContent, "Se puede finalizar con un servicio incompleto");
        var completado = await cliente.GetFromJsonAsync<TurnoRespuesta>(rutaTurno);
        Verificar(completado is { EstadoId: Estado.Completado, Observaciones: "Servicio parcial",
                Servicios: [{ Completado: true }, { Completado: false }],
                Tratamientos: [{ Razon: "Cuidado posterior" }] }
            && completado.Tratamientos[0].TratamientoId == tratamiento.Id
            && completado.FechaSolicitud == turno.FechaSolicitud && completado.FechaEstado >= completado.FechaSolicitud,
            "La finalización conserva la solicitud y guarda el resultado y los tratamientos");
        var capacidadHoy = await cliente.GetFromJsonAsync<CupoDiarioRespuesta>($"/api/cupos-diarios/{cupoHoy.Id}");
        Verificar(capacidadHoy is { CuposOcupados: 3, CuposDisponibles: 0 }, "Completar no libera los cupos utilizados");
        await EsperarCodigo(cliente.PostAsync(rutaTurno + "/cancelar", null), HttpStatusCode.Conflict, "Un turno completado es definitivo");

        var datosFuturos = datos with { CupoId = futuro.Id };
        var rechazado = await SolicitarAsync(datosFuturos);
        Autorizar(cliente, vet.Token);
        await EsperarCodigo(cliente.PostAsync($"{ruta}/{rechazado.Id}/rechazar", null), HttpStatusCode.NoContent, "Rechazar libera capacidad");
        await EsperarCodigo(cliente.PostAsJsonAsync($"{ruta}/{rechazado.Id}/aprobar", new AprobarTurnoSolicitud()),
            HttpStatusCode.Conflict, "Un turno rechazado es definitivo");

        var canceladoPorVet = await SolicitarAsync(datosFuturos);
        Autorizar(cliente, vet.Token);
        await EsperarCodigo(cliente.PostAsJsonAsync($"{ruta}/{canceladoPorVet.Id}/aprobar", new AprobarTurnoSolicitud()),
            HttpStatusCode.NoContent, "Aprobar conserva la cantidad si no se indica otra");
        await EsperarCodigo(cliente.PostAsJsonAsync($"{ruta}/{canceladoPorVet.Id}/completar", new CompletarTurnoSolicitud
        {
            ServicioIdsCompletados = []
        }), HttpStatusCode.Conflict, "Un turno futuro todavía no se puede completar");
        await EsperarCodigo(cliente.PostAsync($"{ruta}/{canceladoPorVet.Id}/cancelar", null), HttpStatusCode.NoContent,
            "El veterinario puede cancelar un turno aprobado");

        var canceladoPorTutor = await SolicitarAsync(datosFuturos);
        Autorizar(cliente, vet.Token);
        await EsperarCodigo(cliente.PostAsJsonAsync($"{ruta}/{canceladoPorTutor.Id}/aprobar", new AprobarTurnoSolicitud()),
            HttpStatusCode.NoContent, "Aprobar el turno que cancelará su tutor");
        Autorizar(cliente, tutor.Token);
        await EsperarCodigo(cliente.PostAsync($"{ruta}/{canceladoPorTutor.Id}/cancelar", null), HttpStatusCode.NoContent,
            "El tutor puede cancelar su turno aprobado");
        var pendiente = await SolicitarAsync(datosFuturos);
        await EsperarCodigo(cliente.PostAsync($"{ruta}/{pendiente.Id}/cancelar", null), HttpStatusCode.NoContent,
            "El tutor también puede cancelar su turno pendiente");
        var capacidadFutura = await cliente.GetFromJsonAsync<CupoDiarioRespuesta>($"/api/cupos-diarios/{futuro.Id}");
        Verificar(capacidadFutura is { CuposOcupados: 0, CuposDisponibles: 1 }, "El cupo queda disponible tras las cancelaciones");

        Autorizar(cliente, tokenAdmin);
        var movimientos = await cliente.GetFromJsonAsync<List<MovimientoAuditoriaRespuesta>>(
            "/api/auditoria/movimientos?entidad=Turno&tamano=100");
        var accionesTurno = movimientos!.Where(m => m.EntidadId == turno.Id.ToString()).Select(m => m.Accion);
        Verificar(accionesTurno.SequenceEqual(new[] { "Completar", "Aprobar", "ModificarServicios", "Solicitar" }),
            "La auditoría del turno incluye solo las operaciones efectivas");
        Verificar(movimientos!.Any(m => m.Accion == "CancelarPorTutor" && m.UsuarioId == tutor.Usuario.Id)
            && movimientos!.Any(m => m.Accion == "CancelarPorVeterinario" && m.UsuarioId == vet.Usuario.Id),
            "La auditoría distingue el rol que canceló");
    }
}
