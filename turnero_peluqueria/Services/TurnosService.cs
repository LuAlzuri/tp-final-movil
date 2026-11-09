using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using turnero_peluqueria.Data;
using turnero_peluqueria.DTOs;
using turnero_peluqueria.Entities;
using turnero_peluqueria.Exceptions;

namespace turnero_peluqueria.Services;

public sealed class TurnosService(AppDbContext db, AuditoriaAccionesService auditoria, VetosService vetos)
{
    private const int CuposPorSolicitud = 1;

    public Task<int> SolicitarAsync(int usuarioId, SolicitarTurnoSolicitud datos, CancellationToken ct) =>
        CrearSolicitudAsync(usuarioId, datos, presencial: false, ct);

    public Task<int> SolicitarPresencialAsync(int usuarioId, SolicitarTurnoSolicitud datos, CancellationToken ct) =>
        CrearSolicitudAsync(usuarioId, datos, presencial: true, ct);

    private async Task<int> CrearSolicitudAsync(int usuarioId, SolicitarTurnoSolicitud datos, bool presencial, CancellationToken ct)
    {
        var servicios = datos.ServicioIds;
        try
        {
            await using var transaccion = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var perros = db.Perros.AsNoTracking().Where(p => !p.Eliminado && !p.Tutor.Eliminado);
            if (presencial)
                await Cuentas.ExigirVeterinarioAsync(db, usuarioId, ct);
            else
                perros = perros.Where(p => p.Tutor.UserId == usuarioId);

            var perro = await perros.SingleOrDefaultAsync(p => p.Id == datos.PerroId, ct)
                ?? throw new ApiException("No se encontró el perro.", StatusCodes.Status404NotFound);
            var veto = await vetos.ObtenerVigenteAsync(perro.TutorId, ct);
            if (presencial && veto is null)
                throw new ApiException("La reserva presencial requiere un tutor con veto vigente.", StatusCodes.Status409Conflict);
            if (!presencial && veto is not null)
                throw SolicitudVetada(veto);

            var cupo = await db.CuposDiarios.AsNoTracking().SingleOrDefaultAsync(c => c.Id == datos.CupoId, ct)
                ?? throw new ApiException("No se encontró el cupo diario.", StatusCodes.Status404NotFound);
            if (cupo.FechaCupo < FechaArgentina.Hoy)
                throw new ApiException("No se pueden solicitar turnos para fechas pasadas.");

            if (cupo.FechaCupo.DayNumber - perro.FechaNacimiento.DayNumber < 45 && !perro.CalVacCom)
                throw new ApiException("Para la fecha solicitada, el perro tendrá menos de 45 días y no tiene el calendario de vacunación completo.");

            if (await db.CuposTurnos.AnyAsync(r => r.CupoId == cupo.Id && r.Turno.PerroId == perro.Id
                && (r.Turno.EstadoId == Estado.Pendiente || r.Turno.EstadoId == Estado.Aprobado), ct))
                throw new ApiException("El perro ya tiene un turno activo para ese día.", StatusCodes.Status409Conflict);

            var ocupados = await ReservasDeCupo.ContarOcupadosAsync(db, cupo.Id, ct);
            if (cupo.NroCupoDis - ocupados < CuposPorSolicitud)
                throw new ApiException("No hay cupos disponibles para ese día.", StatusCodes.Status409Conflict);

            await ValidarServiciosAsync(servicios, ct);

            var ahora = DateTime.UtcNow;
            var turno = new Turno
            {
                PerroId = perro.Id, EstadoId = Estado.Pendiente, FechaSolicitud = ahora, FechaEstado = ahora
            };
            db.CuposTurnos.Add(new CupoTurno { Turno = turno, CupoId = cupo.Id, Cantidad = CuposPorSolicitud });
            db.ServiciosPorTurno.AddRange(servicios.Select(id => new ServicioPorTurno { Turno = turno, ServicioId = id }));
            await db.SaveChangesAsync(ct);
            auditoria.Registrar(presencial ? "SolicitarPresencial" : "Solicitar", nameof(Turno), turno.Id);
            await db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);
            return turno.Id;
        }
        catch (Exception ex) when (ex.GetBaseException() is SqlException { Number: 1205 or 1222 or 3960 })
        {
            throw new ApiException("Hubo una modificación simultánea. Consultá la disponibilidad y volvé a intentar.", StatusCodes.Status409Conflict);
        }
    }

    public Task ModificarServiciosAsync(int id, int usuarioId, ServiciosTurnoSolicitud datos, CancellationToken ct) =>
        EjecutarCambioAsync(async () =>
        {
            var reserva = await BuscarReservaAsync(id, usuarioId, ct);
            ExigirEstado(reserva.Turno, Estado.Pendiente);
            await ValidarServiciosAsync(datos.ServicioIds, ct);

            var actuales = await db.ServiciosPorTurno.Where(s => s.TurnoId == id).ToListAsync(ct);
            db.ServiciosPorTurno.RemoveRange(actuales.Where(s => !datos.ServicioIds.Contains(s.ServicioId)));
            db.ServiciosPorTurno.AddRange(datos.ServicioIds.Except(actuales.Select(s => s.ServicioId))
                .Select(servicioId => new ServicioPorTurno { TurnoId = id, ServicioId = servicioId }));
            auditoria.Registrar("ModificarServicios", nameof(Turno), id);
        }, ct);

    public Task AprobarAsync(int id, int usuarioId, AprobarTurnoSolicitud datos, CancellationToken ct) =>
        EjecutarCambioAsync(async () =>
        {
            var veterinarioId = await Cuentas.ExigirVeterinarioAsync(db, usuarioId, ct);
            var reserva = await BuscarReservaAsync(id, null, ct);
            ExigirEstado(reserva.Turno, Estado.Pendiente);

            var cantidad = datos.Cantidad ?? reserva.Cantidad;
            var ocupados = await ReservasDeCupo.ContarOcupadosAsync(db, reserva.CupoId, ct);
            var ocupadosPorOtrosTurnos = ocupados - reserva.Cantidad;
            if (cantidad > reserva.CupoDiario.NroCupoDis - ocupadosPorOtrosTurnos)
                throw new ApiException("No hay cupos suficientes para aprobar el turno con esa cantidad.", StatusCodes.Status409Conflict);

            reserva.Cantidad = cantidad;
            reserva.Observacion = LimpiarTexto(datos.ObservacionCupo);
            reserva.Turno.VeterinarioId = veterinarioId;
            CambiarEstado(reserva.Turno, Estado.Aprobado, "Aprobar");
        }, ct);

    public Task RechazarAsync(int id, int usuarioId, CancellationToken ct) =>
        CambiarEstadoVeterinarioAsync(id, usuarioId, Estado.Pendiente, Estado.Rechazado, "Rechazar", ct);

    public Task CancelarPorVeterinarioAsync(int id, int usuarioId, CancellationToken ct) =>
        CambiarEstadoVeterinarioAsync(id, usuarioId, Estado.Aprobado, Estado.CanceladoPorVeterinario, "CancelarPorVeterinario", ct);

    public Task CancelarPorTutorAsync(int id, int usuarioId, CancellationToken ct) =>
        EjecutarCambioAsync(async () =>
        {
            var reserva = await BuscarReservaAsync(id, usuarioId, ct);
            if (reserva.Turno.EstadoId is not (Estado.Pendiente or Estado.Aprobado))
                throw EstadoNoPermitido();

            CambiarEstado(reserva.Turno, Estado.CanceladoPorTutor, "CancelarPorTutor");
            var tutorId = await db.Perros.Where(p => p.Id == reserva.Turno.PerroId).Select(p => p.TutorId).SingleAsync(ct);
            await vetos.EvaluarCancelacionAsync(tutorId, id, ct);
        }, ct);

    public Task CompletarAsync(int id, int usuarioId, CompletarTurnoSolicitud datos, CancellationToken ct) =>
        EjecutarCambioAsync(async () =>
        {
            await Cuentas.ExigirVeterinarioAsync(db, usuarioId, ct);
            var reserva = await BuscarReservaAsync(id, null, ct);
            ExigirEstado(reserva.Turno, Estado.Aprobado);
            if (reserva.CupoDiario.FechaCupo > FechaArgentina.Hoy)
                throw new ApiException("No se puede completar el turno antes de su fecha.", StatusCodes.Status409Conflict);

            var servicios = await db.ServiciosPorTurno.Where(s => s.TurnoId == id).ToListAsync(ct);
            var completados = datos.ServicioIdsCompletados.ToHashSet();
            if (completados.Count != datos.ServicioIdsCompletados.Count
                || !completados.IsSubsetOf(servicios.Select(s => s.ServicioId)))
                throw new ApiException("Indicá servicios distintos que pertenezcan al turno.");

            await RegistrarTratamientosAsync(id, datos.Tratamientos, ct);
            foreach (var servicio in servicios)
                servicio.Completado = completados.Contains(servicio.ServicioId);

            reserva.Turno.Observaciones = LimpiarTexto(datos.Observaciones);
            CambiarEstado(reserva.Turno, Estado.Completado, "Completar");
        }, ct);

    private Task CambiarEstadoVeterinarioAsync(int id, int usuarioId, int estadoInicial,
        int estadoFinal, string accion, CancellationToken ct) =>
        EjecutarCambioAsync(async () =>
        {
            await Cuentas.ExigirVeterinarioAsync(db, usuarioId, ct);
            var reserva = await BuscarReservaAsync(id, null, ct);
            ExigirEstado(reserva.Turno, estadoInicial);
            CambiarEstado(reserva.Turno, estadoFinal, accion);
        }, ct);

    private async Task RegistrarTratamientosAsync(int turnoId, List<TratamientoRealizadoSolicitud?> datos, CancellationToken ct)
    {
        if (datos.Count == 0)
            return;
        if (datos.Any(t => t is null))
            throw new ApiException("Cada tratamiento debe tener un identificador válido.");

        var ids = datos.Select(t => t!.TratamientoId).ToList();
        if (ids.Distinct().Count() != ids.Count || await db.Tratamientos.CountAsync(t => ids.Contains(t.Id), ct) != ids.Count)
            throw new ApiException("Los tratamientos indicados deben existir y ser distintos.");

        var actuales = await db.TratamientosPorTurno.Where(t => t.TurnoId == turnoId).ToDictionaryAsync(t => t.TratamientoId, ct);
        foreach (var dato in datos)
        {
            if (!actuales.TryGetValue(dato!.TratamientoId, out var tratamiento))
            {
                tratamiento = new TratamientoPorTurno { TurnoId = turnoId, TratamientoId = dato.TratamientoId };
                db.TratamientosPorTurno.Add(tratamiento);
            }
            tratamiento.Razon = LimpiarTexto(dato.Razon);
        }
    }

    private async Task ValidarServiciosAsync(List<int> servicios, CancellationToken ct)
    {
        if (servicios.Count is < 1 or > ServiciosTurnoSolicitud.MaximoServicios)
            throw new ApiException("Elegí entre 1 y 10 servicios.");
        if (servicios.Any(id => id <= 0) || servicios.Distinct().Count() != servicios.Count)
            throw new ApiException("Los servicios indicados deben ser válidos y distintos.");
        if (await db.Servicios.CountAsync(s => servicios.Contains(s.Id), ct) != servicios.Count)
            throw new ApiException("Uno o más de los servicios indicados no existen.");
    }

    private async Task<CupoTurno> BuscarReservaAsync(int id, int? usuarioTutorId, CancellationToken ct)
    {
        var reservas = db.CuposTurnos.Include(r => r.Turno).Include(r => r.CupoDiario)
            .Where(r => r.TurnoId == id);
        if (usuarioTutorId.HasValue)
            reservas = reservas.Where(r => r.Turno.Perro.Tutor.UserId == usuarioTutorId.Value && !r.Turno.Perro.Tutor.Eliminado);

        return await reservas.SingleOrDefaultAsync(ct)
            ?? throw new ApiException("No se encontró el turno.", StatusCodes.Status404NotFound);
    }

    private async Task EjecutarCambioAsync(Func<Task> preparar, CancellationToken ct)
    {
        try
        {
            await using var transaccion = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await preparar();
            await db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);
        }
        catch (Exception ex) when (ex.GetBaseException() is SqlException { Number: 1205 or 1222 or 2601 or 2627 or 3960 })
        {
            throw new ApiException("Hubo una modificación simultánea. Consultá el turno y volvé a intentar.", StatusCodes.Status409Conflict);
        }
    }

    private void CambiarEstado(Turno turno, int estado, string accion)
    {
        turno.EstadoId = estado;
        var ahora = DateTime.UtcNow;
        if (ahora > turno.FechaEstado)
            turno.FechaEstado = ahora;
        auditoria.Registrar(accion, nameof(Turno), turno.Id);
    }

    private static void ExigirEstado(Turno turno, int estado)
    {
        if (turno.EstadoId != estado)
            throw EstadoNoPermitido();
    }

    private static ApiException EstadoNoPermitido() =>
        new("El estado actual del turno no permite esta operación.", StatusCodes.Status409Conflict);

    private static string? LimpiarTexto(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    private static ApiException SolicitudVetada(Veto veto)
    {
        var mensaje = veto.VeterinarioId is null
            ? "Por reiteradas cancelaciones, solo podés solicitar turnos acercándote a la veterinaria."
            : "Tu cuenta tiene restringida la solicitud de turnos por la app. Acercate a la veterinaria.";
        return new ApiException(mensaje, StatusCodes.Status403Forbidden);
    }
}
