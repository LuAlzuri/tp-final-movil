using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using turnero_peluqueria.Data;
using turnero_peluqueria.Entities;
using turnero_peluqueria.Exceptions;

namespace turnero_peluqueria.Services;

public sealed class BajasService(AppDbContext db, AuditoriaAccionesService auditoria)
{
    private IQueryable<CupoTurno> ReservasActivasDesdeHoy()
    {
        var hoy = FechaArgentina.Hoy;
        return db.CuposTurnos.Where(reserva => reserva.CupoDiario.FechaCupo >= hoy
            && (reserva.Turno.EstadoId == Estado.Pendiente || reserva.Turno.EstadoId == Estado.Aprobado));
    }

    public Task EliminarVeterinarioAsync(int id, CancellationToken ct) => EjecutarBajaAsync(async () =>
    {
        var veterinario = await db.Veterinarios.SingleOrDefaultAsync(v => v.Id == id && !v.Eliminado, ct)
            ?? throw new ApiException("No se encontró el veterinario.", StatusCodes.Status404NotFound);
        if (await ReservasActivasDesdeHoy().AnyAsync(r => r.Turno.VeterinarioId == id, ct))
            throw ConflictoTurnos();

        veterinario.Eliminado = true;
        auditoria.Registrar("Eliminar", nameof(Veterinario), id);
    }, ct);

    public Task EliminarTutorAsync(int id, CancellationToken ct) => EjecutarBajaAsync(async () =>
    {
        var tutor = await db.Tutores.SingleOrDefaultAsync(t => t.Id == id && !t.Eliminado, ct)
            ?? throw new ApiException("No se encontró el tutor.", StatusCodes.Status404NotFound);
        if (await ReservasActivasDesdeHoy().AnyAsync(r => r.Turno.Perro.TutorId == id, ct))
            throw ConflictoTurnos();

        tutor.Eliminado = true;
        auditoria.Registrar("Eliminar", nameof(Tutor), id);
    }, ct);

    public Task EliminarPerroAsync(int id, CancellationToken ct) => EjecutarBajaAsync(async () =>
    {
        var perro = await db.Perros.SingleOrDefaultAsync(p => p.Id == id && !p.Eliminado, ct)
            ?? throw new ApiException("No se encontró el perro.", StatusCodes.Status404NotFound);
        if (await ReservasActivasDesdeHoy().AnyAsync(r => r.Turno.PerroId == id, ct))
            throw ConflictoTurnos();

        perro.Eliminado = true;
        auditoria.Registrar("Eliminar", nameof(Perro), id);
    }, ct);

    private async Task EjecutarBajaAsync(Func<Task> prepararBaja, CancellationToken ct)
    {
        try
        {
            await using var transaccion = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await prepararBaja();
            await db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);
        }
        catch (Exception ex) when (ex.GetBaseException() is SqlException { Number: 1205 or 1222 or 3960 })
        {
            throw new ApiException("Hubo una modificación simultánea. Volvé a intentar la baja.", StatusCodes.Status409Conflict);
        }
    }

    private static ApiException ConflictoTurnos() =>
        new("Hay turnos pendientes o aprobados desde hoy.", StatusCodes.Status409Conflict);
}
