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
    private static async Task VerificarVetosAsync(FabricaApi fabrica, HttpClient cliente, string tokenAdmin)
    {
        const string rutaTurnos = "/api/turnos";
        Autorizar(cliente, null);
        var tutor = await IniciarSesionAsync(cliente, "tutor-otro@turnero.test");
        var vet = await IniciarSesionAsync(cliente, "vet-nuevo@turnero.test");
        var tutorId = tutor.Usuario.TutorId!.Value;
        var rutaVetos = $"/api/tutores/{tutorId}/vetos";
        var hoy = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow,
            "America/Argentina/Buenos_Aires"));

        await using var alcance = fabrica.Services.CreateAsyncScope();
        var db = alcance.ServiceProvider.GetRequiredService<AppDbContext>();
        var perro = new Perro
        {
            Nombre = "Tita", FechaNacimiento = hoy.AddYears(-3), TutorId = tutorId, GrupoEtarioId = 2, TamanioId = 2
        };
        var cupoHoy = await db.CuposDiarios.SingleAsync(c => c.FechaCupo == hoy);
        cupoHoy.NroCupoDis++;
        var cupoFuturo = await db.CuposDiarios.SingleAsync(c => c.FechaCupo == hoy.AddDays(14));
        var anteriores = await db.Turnos.Where(t => t.Perro.TutorId == tutorId
            && t.EstadoId == Estado.CanceladoPorTutor).ToListAsync();
        Verificar(anteriores.Count == 2, "Rechazos y cancelaciones del veterinario no provocan veto");
        var ultimoCompletado = await db.Turnos.Where(t => t.Perro.TutorId == tutorId && t.EstadoId == Estado.Completado)
            .MaxAsync(t => t.FechaEstado);
        foreach (var anterior in anteriores)
        {
            anterior.FechaSolicitud = ultimoCompletado;
            anterior.FechaEstado = ultimoCompletado;
        }
        db.Perros.Add(perro);
        await db.SaveChangesAsync();
        var servicioId = await db.Servicios.OrderBy(s => s.Id).Select(s => s.Id).FirstAsync();
        var datos = new SolicitarTurnoSolicitud { PerroId = perro.Id, CupoId = cupoFuturo.Id, ServicioIds = [servicioId] };

        async Task<TurnoRespuesta> ReservarAsync(string token, string ruta, SolicitarTurnoSolicitud solicitud)
        {
            Autorizar(cliente, token);
            using var respuesta = await cliente.PostAsJsonAsync(ruta, solicitud);
            Verificar(respuesta.StatusCode == HttpStatusCode.Created, "La reserva válida debe guardarse");
            return (await respuesta.Content.ReadFromJsonAsync<TurnoRespuesta>())!;
        }

        async Task CancelarComoTutorAsync(int turnoId)
        {
            Autorizar(cliente, tutor.Token);
            await EsperarCodigo(cliente.PostAsync($"{rutaTurnos}/{turnoId}/cancelar", null), HttpStatusCode.NoContent,
                "El tutor puede cancelar su turno");
        }

        var tercero = await ReservarAsync(tutor.Token, rutaTurnos, datos);
        await CancelarComoTutorAsync(tercero.Id);
        var automatico = await db.Vetos.AsNoTracking().SingleAsync(v => v.TutorId == tutorId);
        Verificar(automatico is { VeterinarioId: null, FechaFin: null, Motivo: Veto.MotivoAutomatico },
            "La tercera cancelación entre perros distintos aplica el veto incluso si los movimientos previos comparten fecha");
        using (var bloqueada = await cliente.PostAsJsonAsync(rutaTurnos, datos))
        {
            Verificar(bloqueada.StatusCode == HttpStatusCode.Forbidden
                && (await bloqueada.Content.ReadFromJsonAsync<Dictionary<string, string>>())?["error"]
                    == "Por reiteradas cancelaciones, solo podés solicitar turnos acercándote a la veterinaria.",
                "La app recibe el mensaje del veto automático");
        }
        await EsperarCodigo(cliente.GetAsync($"{rutaTurnos}/{tercero.Id}"), HttpStatusCode.OK, "El veto conserva el acceso al historial");
        await EsperarCodigo(cliente.PostAsJsonAsync(rutaTurnos + "/presencial", datos), HttpStatusCode.Forbidden,
            "La reserva presencial requiere un veterinario");
        await EsperarCodigo(cliente.GetAsync(rutaVetos), HttpStatusCode.Forbidden, "El historial de vetos es de uso veterinario");
        await EsperarCodigo(cliente.GetAsync(rutaVetos + "/vigente"), HttpStatusCode.Forbidden, "El tutor no consulta el estado interno de vetos");

        Autorizar(cliente, vet.Token);
        Verificar((await cliente.GetFromJsonAsync<EstadoVetoRespuesta>(rutaVetos + "/vigente"))?.Veto?.Id == automatico.Id,
            "El veto vigente se consulta sin depender del historial paginado");
        await EsperarCodigo(cliente.PostAsync(rutaVetos, null), HttpStatusCode.Conflict, "Solo puede existir un veto vigente");
        var presencial = await ReservarAsync(vet.Token, rutaTurnos + "/presencial", datos);
        Verificar(presencial is { EstadoId: Estado.Aprobado, EsPresencial: true, CuposReservados: 1 }
            && presencial.VeterinarioId == vet.Usuario.VeterinarioId, "La reserva presencial nace aprobada con el veterinario responsable");
        Autorizar(cliente, tutor.Token);
        await EsperarCodigo(cliente.PostAsync($"{rutaTurnos}/{presencial.Id}/cancelar", null), HttpStatusCode.Forbidden,
            "El tutor no puede cancelar una reserva presencial aun llamando directamente a la API");
        var visible = await cliente.GetFromJsonAsync<TurnoRespuesta>($"{rutaTurnos}/{presencial.Id}");
        Verificar(visible is { EstadoId: Estado.Aprobado, EsPresencial: true, CuposReservados: 1 },
            "La cancelación prohibida conserva el turno y la reserva y permite consultarlos");
        Autorizar(cliente, vet.Token);
        await EsperarCodigo(cliente.PostAsJsonAsync($"{rutaVetos}/{automatico.Id}/levantar", new MotivoVetoSolicitud { Motivo = " Revisión presencial " }),
            HttpStatusCode.NoContent, "El veterinario puede levantar el veto");
        await EsperarCodigo(cliente.PostAsync($"{rutaVetos}/{automatico.Id}/levantar", null),
            HttpStatusCode.Conflict, "Una segunda solicitud no cambia la fecha de levantamiento");
        await EsperarCodigo(cliente.PostAsJsonAsync(rutaTurnos + "/presencial", datos),
            HttpStatusCode.Conflict, "La reserva presencial se usa para tutores con veto vigente");

        Autorizar(cliente, tutor.Token);
        await EsperarCodigo(cliente.PostAsync($"{rutaTurnos}/{presencial.Id}/cancelar", null), HttpStatusCode.Forbidden,
            "Levantar el veto no habilita la cancelación del turno presencial por el tutor");
        Autorizar(cliente, vet.Token);
        await EsperarCodigo(cliente.PostAsync($"{rutaTurnos}/{presencial.Id}/cancelar", null), HttpStatusCode.NoContent,
            "El veterinario conserva la cancelación de las reservas presenciales");
        await CancelarComoTutorAsync((await ReservarAsync(tutor.Token, rutaTurnos, datos)).Id);
        await CancelarComoTutorAsync((await ReservarAsync(tutor.Token, rutaTurnos, datos)).Id);
        Verificar(!await db.Vetos.AnyAsync(v => v.TutorId == tutorId && v.FechaFin == null),
            "Levantar el veto reinicia el conteo");

        var atendido = await ReservarAsync(tutor.Token, rutaTurnos, datos with { CupoId = cupoHoy.Id });
        Autorizar(cliente, vet.Token);
        await EsperarCodigo(cliente.PostAsJsonAsync($"{rutaTurnos}/{atendido.Id}/aprobar", new AprobarTurnoSolicitud()),
            HttpStatusCode.NoContent, "Aprobación antes de completar");
        await EsperarCodigo(cliente.PostAsJsonAsync($"{rutaTurnos}/{atendido.Id}/completar", new CompletarTurnoSolicitud
        {
            ServicioIdsCompletados = [servicioId]
        }), HttpStatusCode.NoContent, "Completar el turno reinicia el conteo");
        await CancelarComoTutorAsync((await ReservarAsync(tutor.Token, rutaTurnos, datos)).Id);
        Verificar(!await db.Vetos.AnyAsync(v => v.TutorId == tutorId && v.FechaFin == null),
            "La cancelación posterior al turno completado cuenta como primera");

        Autorizar(cliente, vet.Token);
        using var altaManual = await cliente.PostAsync(rutaVetos, null);
        Verificar(altaManual.StatusCode == HttpStatusCode.Created && altaManual.Headers.Location is not null,
            "Se puede aplicar un veto manual sin motivo");
        var manual = (await altaManual.Content.ReadFromJsonAsync<VetoRespuesta>())!;
        Verificar(manual.VeterinarioId == vet.Usuario.VeterinarioId && manual.Motivo is null, "El veto manual registra al veterinario");
        Autorizar(cliente, tutor.Token);
        using (var bloqueada = await cliente.PostAsJsonAsync(rutaTurnos, datos))
        {
            Verificar(bloqueada.StatusCode == HttpStatusCode.Forbidden
                && (await bloqueada.Content.ReadFromJsonAsync<Dictionary<string, string>>())?["error"]
                    == "Tu cuenta tiene restringida la solicitud de turnos por la app. Acercate a la veterinaria.",
                "La app recibe el mensaje del veto manual");
        }
        Autorizar(cliente, vet.Token);
        await EsperarCodigo(cliente.PostAsync($"{rutaVetos}/{manual.Id}/levantar", null), HttpStatusCode.NoContent,
            "Levantar un veto no exige motivo");
        var historial = await cliente.GetFromJsonAsync<List<VetoRespuesta>>(rutaVetos);
        Verificar(historial is [{ FechaFin: not null }, { FechaFin: not null, MotivoFinVeto: "Revisión presencial" }],
            "El historial conserva ambos vetos y el motivo informado");

        Autorizar(cliente, tokenAdmin);
        var movimientos = await cliente.GetFromJsonAsync<List<MovimientoAuditoriaRespuesta>>("/api/auditoria/movimientos?entidad=Veto");
        Verificar(movimientos is [{ Accion: "LevantarVeto" }, { Accion: "AplicarVeto" }, { Accion: "LevantarVeto" },
            { Accion: "AplicarVetoAutomatico", UsuarioId: null }]
            && movimientos.Take(3).All(a => a.UsuarioId == vet.Usuario.Id),
            "La auditoría distingue los movimientos del veterinario y del sistema");
        var solicitudes = await cliente.GetFromJsonAsync<List<MovimientoAuditoriaRespuesta>>("/api/auditoria/movimientos?entidad=Turno&tamano=100");
        Verificar(solicitudes?.Any(a => a.Accion == "SolicitarPresencial"
            && a.EntidadId == presencial.Id.ToString() && a.UsuarioId == vet.Usuario.Id) == true,
            "La reserva presencial queda auditada a nombre del veterinario");
        Verificar(solicitudes?.Count(a => a.Accion == "Aprobar" && a.EntidadId == presencial.Id.ToString()
            && a.UsuarioId == vet.Usuario.Id) == 1, "La aprobación directa queda auditada una única vez");
        Verificar(solicitudes?.Any(a => a.Accion == "CancelarPorTutor" && a.EntidadId == presencial.Id.ToString()) == false,
            "Los intentos bloqueados no generan cancelaciones en auditoría");

        await EsperarCodigo(cliente.GetAsync(rutaVetos + "/vigente"), HttpStatusCode.Forbidden,
            "El administrador tampoco accede a los vetos exclusivos del veterinario");
        Autorizar(cliente, null);
        await EsperarCodigo(cliente.GetAsync(rutaVetos + "/vigente"), HttpStatusCode.Unauthorized,
            "Consultar veto vigente requiere autenticación");
        Autorizar(cliente, vet.Token);
        Verificar((await cliente.GetFromJsonAsync<EstadoVetoRespuesta>(rutaVetos + "/vigente"))?.Veto is null,
            "El estado sin veto tiene una respuesta explícita");
        await EsperarCodigo(cliente.PostAsJsonAsync(rutaVetos, new MotivoVetoSolicitud { Motivo = new string('a', 501) }),
            HttpStatusCode.BadRequest, "El motivo conserva el máximo de 500 caracteres");
        using var altaLimite = await cliente.PostAsJsonAsync(rutaVetos, new MotivoVetoSolicitud { Motivo = new string('a', 500) });
        Verificar(altaLimite.StatusCode == HttpStatusCode.Created, "El motivo acepta exactamente 500 caracteres");
        var limite = (await altaLimite.Content.ReadFromJsonAsync<VetoRespuesta>())!;

        // Una página llena de registros finalizados nunca debe ocultar el veto vigente.
        var cerrados = Enumerable.Range(0, 21).Select(i => new Veto
        {
            TutorId = tutorId, VeterinarioId = vet.Usuario.VeterinarioId,
            FechaInicio = DateTime.UtcNow.AddMinutes(i + 1), FechaFin = DateTime.UtcNow.AddMinutes(i + 2)
        }).ToList();
        db.Vetos.AddRange(cerrados);
        await db.SaveChangesAsync();
        var primera = await cliente.GetFromJsonAsync<List<VetoRespuesta>>(rutaVetos + "?pagina=1&tamano=20");
        Verificar(primera?.Count == 20 && primera.All(v => v.FechaFin is not null), "La primera página puede no contener el veto vigente");
        Verificar((await cliente.GetFromJsonAsync<EstadoVetoRespuesta>(rutaVetos + "/vigente"))?.Veto?.Id == limite.Id,
            "La consulta vigente encuentra vetos fuera de la primera página");
        db.Vetos.RemoveRange(cerrados);
        cupoHoy.NroCupoDis += 2;
        await db.SaveChangesAsync();
        var atencionVetada = await ReservarAsync(vet.Token, rutaTurnos + "/presencial", datos with { CupoId = cupoHoy.Id });
        Verificar(atencionVetada.EstadoId == Estado.Aprobado, "La reserva presencial puede completarse sin otra aprobación");
        await EsperarCodigo(cliente.PostAsJsonAsync($"{rutaTurnos}/{atencionVetada.Id}/completar", new CompletarTurnoSolicitud { ServicioIdsCompletados = [servicioId] }),
            HttpStatusCode.NoContent, "Se puede completar una atención con veto vigente");
        Verificar((await cliente.GetFromJsonAsync<EstadoVetoRespuesta>(rutaVetos + "/vigente"))?.Veto?.Id == limite.Id,
            "Completar la atención no levanta el veto vigente");
        await EsperarCodigo(cliente.PostAsJsonAsync($"{rutaVetos}/{limite.Id}/levantar", new MotivoVetoSolicitud { Motivo = new string('a', 501) }),
            HttpStatusCode.BadRequest, "El levantamiento también valida el máximo del motivo");
        await EsperarCodigo(cliente.PostAsync($"{rutaVetos}/{limite.Id}/levantar", null), HttpStatusCode.NoContent,
            "El levantamiento permite volver a reservar");
        await ReservarAsync(tutor.Token, rutaTurnos, datos);
    }
}
