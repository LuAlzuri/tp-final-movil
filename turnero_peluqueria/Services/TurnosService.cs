using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using turnero_peluqueria.Data;
using turnero_peluqueria.DTOs;
using turnero_peluqueria.Entities;
using turnero_peluqueria.Exceptions;

namespace turnero_peluqueria.Services;

public sealed class TurnosService(AppDbContext db, AuditoriaAccionesService auditoria)
{
    private const int CuposPorSolicitud = 1;

    public async Task<int> SolicitarAsync(int usuarioId, SolicitarTurnoSolicitud datos, CancellationToken ct)
    {
        var servicios = datos.ServicioIds;
        if (servicios.Any(id => id <= 0) || servicios.Distinct().Count() != servicios.Count)
            throw new ApiException("Los servicios indicados deben ser válidos y distintos.");

        try
        {
            await using var transaccion = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var perro = await db.Perros.AsNoTracking().SingleOrDefaultAsync(p =>
                p.Id == datos.PerroId && !p.Eliminado && !p.Tutor.Eliminado && p.Tutor.UserId == usuarioId, ct)
                ?? throw new ApiException("No se encontró el perro.", StatusCodes.Status404NotFound);
            await ExigirTutorSinVetoAsync(perro.TutorId, ct);

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

            if (await db.Servicios.CountAsync(s => servicios.Contains(s.Id), ct) != servicios.Count)
                throw new ApiException("Uno o más de los servicios indicados no existen.");

            var ahora = DateTime.UtcNow;
            var turno = new Turno
            {
                PerroId = perro.Id, EstadoId = Estado.Pendiente, FechaSolicitud = ahora, FechaEstado = ahora
            };
            db.CuposTurnos.Add(new CupoTurno { Turno = turno, CupoId = cupo.Id, Cantidad = CuposPorSolicitud });
            db.ServiciosPorTurno.AddRange(servicios.Select(id => new ServicioPorTurno { Turno = turno, ServicioId = id }));
            await db.SaveChangesAsync(ct);
            auditoria.Registrar("Solicitar", nameof(Turno), turno.Id);
            await db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);
            return turno.Id;
        }
        catch (Exception ex) when (ex.GetBaseException() is SqlException { Number: 1205 or 1222 or 3960 })
        {
            throw new ApiException("Hubo una modificación simultánea. Consultá la disponibilidad y volvé a intentar.", StatusCodes.Status409Conflict);
        }
    }

    private async Task ExigirTutorSinVetoAsync(int tutorId, CancellationToken ct)
    {
        var veto = await db.Vetos.AsNoTracking().SingleOrDefaultAsync(v => v.TutorId == tutorId && v.FechaFin == null, ct);
        if (veto is null)
            return;

        var mensaje = veto.VeterinarioId is null
            ? "Por reiteradas cancelaciones, solo podés solicitar turnos acercándote a la veterinaria."
            : "Tu cuenta tiene restringida la solicitud de turnos por la app. Acercate a la veterinaria.";
        throw new ApiException(mensaje, StatusCodes.Status403Forbidden);
    }
}
