using Microsoft.EntityFrameworkCore;
using turnero_peluqueria.Entities;

namespace turnero_peluqueria.Data;

internal static class ReservasDeCupo
{
    public static IQueryable<CupoTurno> QueOcupan(AppDbContext db) =>
        db.CuposTurnos.Where(r => r.Turno.EstadoId == Estado.Pendiente
            || r.Turno.EstadoId == Estado.Aprobado || r.Turno.EstadoId == Estado.Completado);

    public static async Task<int> ContarOcupadosAsync(AppDbContext db, int cupoId, CancellationToken ct) =>
        await QueOcupan(db).Where(r => r.CupoId == cupoId).SumAsync(r => (int?)r.Cantidad, ct) ?? 0;
}
