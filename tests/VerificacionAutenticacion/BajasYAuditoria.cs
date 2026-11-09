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
    private static async Task VerificarBajasYAuditoriaAsync(FabricaApi fabrica, HttpClient cliente,
        SesionRespuesta admin, SesionRespuesta vet)
    {
        Autorizar(cliente, null);
        var tutor = await IniciarSesionAsync(cliente, "tutor-nuevo@turnero.test");
        var tutorId = tutor.Usuario.TutorId!.Value;
        var veterinarioId = vet.Usuario.VeterinarioId!.Value;
        var rutaTutor = $"/api/tutores/{tutorId}";
        var rutaVeterinario = $"/api/veterinarios/{veterinarioId}";
        const string rutaAuditoria = "/api/auditoria/movimientos";

        int turnoId;
        int telefonoId;
        await using (var alcance = fabrica.Services.CreateAsyncScope())
        {
            var db = alcance.ServiceProvider.GetRequiredService<AppDbContext>();
            var hoy = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow,
                "America/Argentina/Buenos_Aires"));
            var reserva = new CupoTurno
            {
                CupoDiario = new CupoDiario { FechaCupo = hoy, NroCupoDis = 1 },
                Turno = new Turno
                {
                    VeterinarioId = veterinarioId, EstadoId = Estado.Aprobado,
                    Perro = new Perro { TutorId = tutorId, Nombre = "Luna", FechaNacimiento = hoy.AddYears(-3), GrupoEtarioId = 2, TamanioId = 2 }
                }
            };
            db.CuposTurnos.Add(reserva);
            await db.SaveChangesAsync();
            turnoId = reserva.TurnoId;
            telefonoId = await db.TelefonosTutores.Where(t => t.TutorId == tutorId)
                .OrderBy(t => t.Id).Select(t => t.Id).FirstAsync();
        }

        Autorizar(cliente, vet.Token);
        await EsperarCodigo(cliente.GetAsync(rutaAuditoria), HttpStatusCode.Forbidden, "Auditoría reservada al administrador");
        await EsperarCodigo(cliente.DeleteAsync(rutaTutor), HttpStatusCode.Conflict, "El turno aprobado de hoy bloquea la baja del tutor");
        await EsperarCodigo(cliente.DeleteAsync($"{rutaTutor}/telefonos/{telefonoId}"), HttpStatusCode.NoContent, "Borrado de teléfono");
        Autorizar(cliente, admin.Token);
        await EsperarCodigo(cliente.DeleteAsync(rutaVeterinario), HttpStatusCode.Conflict, "El turno aprobado de hoy bloquea la baja del veterinario");

        await using (var alcance = fabrica.Services.CreateAsyncScope())
        {
            var db = alcance.ServiceProvider.GetRequiredService<AppDbContext>();
            Verificar(!await db.AuditoriaAcciones.AnyAsync(a => a.Accion == "Eliminar" && a.Entidad != nameof(TelefonoTutor)),
                "Los intentos bloqueados no deben quedar registrados como bajas");
            var turno = await db.Turnos.SingleAsync(t => t.Id == turnoId);
            turno.EstadoId = Estado.Completado;
            await db.SaveChangesAsync();
        }

        Autorizar(cliente, vet.Token);
        await EsperarCodigo(cliente.DeleteAsync(rutaTutor), HttpStatusCode.NoContent, "Baja lógica del tutor");
        Autorizar(cliente, tutor.Token);
        await EsperarCodigo(cliente.GetAsync(Ruta + "mi-perfil"), HttpStatusCode.Unauthorized, "La baja del tutor invalida su sesión");
        Autorizar(cliente, admin.Token);
        await EsperarCodigo(cliente.DeleteAsync(rutaVeterinario), HttpStatusCode.NoContent, "Baja lógica del veterinario");
        Autorizar(cliente, vet.Token);
        await EsperarCodigo(cliente.GetAsync(Ruta + "mi-perfil"), HttpStatusCode.Unauthorized, "La baja del veterinario invalida su sesión");

        Autorizar(cliente, admin.Token);
        var movimientos = await cliente.GetFromJsonAsync<List<MovimientoAuditoriaRespuesta>>(rutaAuditoria + "?tamano=100");
        Verificar(movimientos?.Any(a => a.Accion == "Crear" && a.Entidad == nameof(Tutor) && a.UsuarioId == vet.Usuario.Id) == true,
            "El alta de tutor debe registrar al veterinario que la hizo");
        Verificar(movimientos?.Any(a => a.Accion == "Modificar" && a.Entidad == nameof(Veterinario) && a.UsuarioId == admin.Usuario.Id) == true,
            "La edición de veterinario debe registrar al administrador");
        Verificar(movimientos?.Count(a => a.Accion == "Eliminar") == 3, "Cada baja efectiva debe registrar un movimiento");
        var filtrados = await cliente.GetFromJsonAsync<List<MovimientoAuditoriaRespuesta>>(
            $"{rutaAuditoria}?entidad=Veterinario&usuarioId={admin.Usuario.Id}&tamano=1");
        Verificar(filtrados is [{ Accion: "Eliminar", Entidad: nameof(Veterinario) }],
            "Los filtros y el tamaño deben devolver el movimiento más reciente del administrador");

        await using var comprobacion = fabrica.Services.CreateAsyncScope();
        var datos = comprobacion.ServiceProvider.GetRequiredService<AppDbContext>();
        Verificar(await datos.Turnos.AnyAsync(t => t.Id == turnoId && t.Perro.TutorId == tutorId),
            "Las bajas deben conservar perros y turnos");
        Verificar(await datos.TelefonosTutores.CountAsync(t => t.TutorId == tutorId) == 1
            && !await datos.TelefonosTutores.AnyAsync(t => t.Id == telefonoId),
            "Solo debe borrarse el teléfono seleccionado");
    }
}
