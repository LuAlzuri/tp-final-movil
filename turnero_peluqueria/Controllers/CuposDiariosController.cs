using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using turnero_peluqueria.Data;
using turnero_peluqueria.DTOs;
using turnero_peluqueria.Entities;
using turnero_peluqueria.Exceptions;
using turnero_peluqueria.Services;

namespace turnero_peluqueria.Controllers;

[ApiController]
[Route("api/cupos-diarios")]
[Authorize(Roles = Usuario.RolVeterinario + "," + Usuario.RolTutor)]
public class CuposDiariosController(AppDbContext db, AuditoriaAccionesService auditoria) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CupoDiarioRespuesta>>> Listar(
        CancellationToken ct, DateOnly? desde = null, DateOnly? hasta = null,
        [Range(1, 1000000)] int pagina = 1, [Range(1, 100)] int tamano = 20)
    {
        if (desde.HasValue && hasta.HasValue && desde.Value > hasta.Value)
            throw new ApiException("La fecha inicial no puede ser posterior a la fecha final.");

        var cupos = db.CuposDiarios.AsNoTracking();
        if (desde.HasValue)
            cupos = cupos.Where(c => c.FechaCupo >= desde.Value);
        if (hasta.HasValue)
            cupos = cupos.Where(c => c.FechaCupo <= hasta.Value);

        return await Consultar(cupos, pagina, tamano).ToListAsync(ct);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CupoDiarioRespuesta>> ObtenerPorId(int id, CancellationToken ct) =>
        await Consultar(db.CuposDiarios.AsNoTracking().Where(c => c.Id == id)).SingleOrDefaultAsync(ct) ?? throw NoEncontrado();

    [HttpPost]
    [Authorize(Roles = Usuario.RolVeterinario)]
    public async Task<ActionResult<CupoDiarioRespuesta>> Publicar(PublicarCupoSolicitud datos, CancellationToken ct)
    {
        if (datos.FechaCupo < FechaArgentina.Hoy)
            throw new ApiException("No se pueden publicar cupos para fechas pasadas.");
        if (await db.CuposDiarios.AnyAsync(c => c.FechaCupo == datos.FechaCupo, ct))
            throw new ApiException("Ya hay un cupo diario publicado para esa fecha.", StatusCodes.Status409Conflict);

        var cupo = new CupoDiario { FechaCupo = datos.FechaCupo, NroCupoDis = datos.CuposPublicados };
        db.CuposDiarios.Add(cupo);
        await auditoria.GuardarAltaAsync(() => auditoria.Registrar("Crear", nameof(CupoDiario), cupo.Id), ct);

        return CreatedAtAction(nameof(ObtenerPorId), new { id = cupo.Id },
            new CupoDiarioRespuesta(cupo.Id, cupo.FechaCupo, cupo.NroCupoDis, 0, cupo.NroCupoDis));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Usuario.RolVeterinario)]
    public async Task<IActionResult> Modificar(int id, CapacidadCupoSolicitud datos, CancellationToken ct)
    {
        await EjecutarCambioAsync(async () =>
        {
            var cupo = await BuscarAsync(id, ct);
            var ocupados = await ReservasDeCupo.ContarOcupadosAsync(db, id, ct);
            if (datos.CuposPublicados < ocupados)
                throw new ApiException("La capacidad no puede ser menor a los cupos ocupados.", StatusCodes.Status409Conflict);

            cupo.NroCupoDis = datos.CuposPublicados;
            auditoria.Registrar("Modificar", nameof(CupoDiario), id);
        }, ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Usuario.RolVeterinario)]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        await EjecutarCambioAsync(async () =>
        {
            var cupo = await BuscarAsync(id, ct);
            if (await db.CuposTurnos.AnyAsync(r => r.CupoId == id, ct))
                throw EnUso();

            db.CuposDiarios.Remove(cupo);
            auditoria.Registrar("Eliminar", nameof(CupoDiario), id);
        }, ct);
        return NoContent();
    }

    private IQueryable<CupoDiarioRespuesta> Consultar(IQueryable<CupoDiario> cupos, int? pagina = null, int tamano = 20)
    {
        var soloDisponibles = User.IsInRole(Usuario.RolTutor);
        if (soloDisponibles)
        {
            var hoy = FechaArgentina.Hoy;
            cupos = cupos.Where(c => c.FechaCupo >= hoy);
        }

        var reservas = ReservasDeCupo.QueOcupan(db);
        var conOcupacion = cupos.Select(c => new
        {
            c.Id,
            c.FechaCupo,
            c.NroCupoDis,
            Ocupados = reservas.Where(r => r.CupoId == c.Id).Sum(r => (int?)r.Cantidad) ?? 0
        });
        if (soloDisponibles)
            conOcupacion = conOcupacion.Where(c => c.Ocupados < c.NroCupoDis);
        if (pagina.HasValue)
            conOcupacion = conOcupacion.OrderBy(c => c.FechaCupo).Skip((pagina.Value - 1) * tamano).Take(tamano);

        return conOcupacion.Select(c => new CupoDiarioRespuesta(c.Id, c.FechaCupo, c.NroCupoDis,
            c.Ocupados, c.NroCupoDis - c.Ocupados));
    }

    private async Task<CupoDiario> BuscarAsync(int id, CancellationToken ct) =>
        await db.CuposDiarios.SingleOrDefaultAsync(c => c.Id == id, ct) ?? throw NoEncontrado();

    private async Task EjecutarCambioAsync(Func<Task> preparar, CancellationToken ct)
    {
        try
        {
            await using var transaccion = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await preparar();
            await db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.GetBaseException() is SqlException { Number: 547 })
        {
            throw EnUso();
        }
        catch (Exception ex) when (ex.GetBaseException() is SqlException { Number: 1205 or 1222 or 3960 })
        {
            throw new ApiException("Hubo una modificación simultánea. Volvé a intentar la operación.", StatusCodes.Status409Conflict);
        }
    }

    private static ApiException NoEncontrado() =>
        new("No se encontró el cupo diario.", StatusCodes.Status404NotFound);

    private static ApiException EnUso() =>
        new("El cupo diario tiene turnos asociados.", StatusCodes.Status409Conflict);
}
