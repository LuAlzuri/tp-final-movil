using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using turnero_peluqueria.Data;
using turnero_peluqueria.Entities;
using turnero_peluqueria.Exceptions;

namespace turnero_peluqueria.Services;

public sealed class VetosService(AppDbContext db, AuditoriaAccionesService auditoria)
{
    private const int CancelacionesParaVeto = 3;

    internal Task<Veto?> ObtenerVigenteAsync(int tutorId, CancellationToken ct) =>
        db.Vetos.AsNoTracking().SingleOrDefaultAsync(v => v.TutorId == tutorId && v.FechaFin == null, ct);

    public async Task<Veto> AplicarAsync(int tutorId, int usuarioId, string? motivo, CancellationToken ct)
    {
        try
        {
            await using var transaccion = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var veterinarioId = await Cuentas.ExigirVeterinarioAsync(db, usuarioId, ct);
            if (!await db.Tutores.AnyAsync(t => t.Id == tutorId && !t.Eliminado, ct))
                throw new ApiException("No se encontró el tutor activo.", StatusCodes.Status404NotFound);
            if (await ObtenerVigenteAsync(tutorId, ct) is not null)
                throw new ApiException("El tutor ya tiene un veto vigente.", StatusCodes.Status409Conflict);

            var veto = new Veto { TutorId = tutorId, VeterinarioId = veterinarioId, Motivo = LimpiarMotivo(motivo) };
            db.Vetos.Add(veto);
            await db.SaveChangesAsync(ct);
            auditoria.Registrar("AplicarVeto", nameof(Veto), veto.Id);
            await db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);
            return veto;
        }
        catch (Exception ex) when (EsConflictoSql(ex))
        {
            throw Conflicto();
        }
    }

    public async Task LevantarAsync(int tutorId, int vetoId, int usuarioId, string? motivo, CancellationToken ct)
    {
        try
        {
            await using var transaccion = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await Cuentas.ExigirVeterinarioAsync(db, usuarioId, ct);
            var veto = await db.Vetos.SingleOrDefaultAsync(v => v.Id == vetoId && v.TutorId == tutorId, ct)
                ?? throw new ApiException("No se encontró el veto del tutor.", StatusCodes.Status404NotFound);
            if (veto.FechaFin.HasValue)
                throw new ApiException("El veto ya fue levantado.", StatusCodes.Status409Conflict);

            var ahora = DateTime.UtcNow;
            veto.FechaFin = ahora < veto.FechaInicio ? veto.FechaInicio : ahora;
            veto.MotivoFinVeto = LimpiarMotivo(motivo);
            auditoria.Registrar("LevantarVeto", nameof(Veto), veto.Id);
            await db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);
        }
        catch (Exception ex) when (EsConflictoSql(ex))
        {
            throw Conflicto();
        }
    }

    // Se ejecuta en la transacción de la cancelación. El turno actual todavía no está guardado.
    internal async Task EvaluarCancelacionAsync(int tutorId, int turnoId, CancellationToken ct)
    {
        if (await ObtenerVigenteAsync(tutorId, ct) is not null)
            return;

        var turnos = db.Turnos.Where(t => t.Perro.TutorId == tutorId);
        var vetos = db.Vetos.Where(v => v.TutorId == tutorId);
        var ultimoCompletado = await turnos.Where(t => t.EstadoId == Estado.Completado)
            .MaxAsync(t => (DateTime?)t.FechaEstado, ct);
        var ultimoLevantamiento = await vetos.MaxAsync(v => v.FechaFin, ct);
        var reinicio = ultimoLevantamiento > ultimoCompletado || ultimoCompletado is null
            ? ultimoLevantamiento : ultimoCompletado;

        var cancelaciones = turnos.Where(t => t.EstadoId == Estado.CanceladoPorTutor && t.Id != turnoId);
        if (reinicio.HasValue)
        {
            var fecha = reinicio.Value;
            var completados = turnos.Where(t => t.EstadoId == Estado.Completado && t.FechaEstado == fecha)
                .Select(t => t.Id.ToString());
            var levantados = vetos.Where(v => v.FechaFin == fecha).Select(v => v.Id.ToString());

            // SQL Server puede guardar dos movimientos con la misma fecha. La auditoría conserva su orden.
            var ordenReinicio = await db.AuditoriaAcciones.Where(a => a.EntidadId != null &&
                ((a.Entidad == nameof(Turno) && a.Accion == "Completar" && completados.Contains(a.EntidadId))
                || (a.Entidad == nameof(Veto) && a.Accion == "LevantarVeto" && levantados.Contains(a.EntidadId))))
                .MaxAsync(a => (long?)a.Id, ct);

            cancelaciones = ordenReinicio is long orden
                ? cancelaciones.Where(t => t.FechaEstado > fecha
                || (t.FechaEstado == fecha && db.AuditoriaAcciones.Any(a =>
                    a.Entidad == nameof(Turno) && a.Accion == "CancelarPorTutor"
                    && a.EntidadId == t.Id.ToString() && a.Id > orden)))
                : cancelaciones.Where(t => t.FechaEstado > fecha);
        }

        var anteriores = await cancelaciones.OrderBy(t => t.Id).Take(CancelacionesParaVeto - 1).CountAsync(ct);
        if (anteriores + 1 < CancelacionesParaVeto)
            return;

        var veto = new Veto { TutorId = tutorId, Motivo = Veto.MotivoAutomatico };
        db.Vetos.Add(veto);
        await db.SaveChangesAsync(ct);
        auditoria.Registrar("AplicarVetoAutomatico", nameof(Veto), veto.Id, automatico: true);
    }

    private static bool EsConflictoSql(Exception ex) =>
        ex.GetBaseException() is SqlException { Number: 1205 or 1222 or 2601 or 2627 or 3960 };

    private static ApiException Conflicto() =>
        new("Hubo una modificación simultánea. Consultá el historial de vetos y volvé a intentar.", StatusCodes.Status409Conflict);

    private static string? LimpiarMotivo(string? motivo) => string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim();
}
