using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using turnero_peluqueria.Data;
using turnero_peluqueria.DTOs;
using turnero_peluqueria.Entities;
using turnero_peluqueria.Exceptions;
using turnero_peluqueria.Security;
using turnero_peluqueria.Services;

namespace turnero_peluqueria.Controllers;

[ApiController]
[Route("api/turnos")]
[Authorize(Roles = Usuario.RolVeterinario + "," + Usuario.RolTutor)]
public class TurnosController(AppDbContext db, TurnosService turnos) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<TurnoRespuesta>>> Listar(CancellationToken ct,
        [Range(1, 1000000)] int pagina = 1, [Range(1, 100)] int tamano = 20,
        [Range(1, int.MaxValue)] int? perroId = null, [Range(1, 6)] int? estadoId = null, DateOnly? fechaCupo = null)
    {
        var reservas = ReservasVisibles();
        if (perroId.HasValue)
            reservas = reservas.Where(r => r.Turno.PerroId == perroId.Value);
        if (estadoId.HasValue)
            reservas = reservas.Where(r => r.Turno.EstadoId == estadoId.Value);
        if (fechaCupo.HasValue)
            reservas = reservas.Where(r => r.CupoDiario.FechaCupo == fechaCupo.Value);

        return await Proyectar(reservas.OrderByDescending(r => r.CupoDiario.FechaCupo).ThenByDescending(r => r.TurnoId)
            .Skip((pagina - 1) * tamano).Take(tamano)).ToListAsync(ct);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TurnoRespuesta>> ObtenerPorId(int id, CancellationToken ct) =>
        await BuscarRespuestaAsync(id, ct);

    [HttpPost]
    [Authorize(Roles = Usuario.RolTutor)]
    public async Task<ActionResult<TurnoRespuesta>> Solicitar(SolicitarTurnoSolicitud datos, CancellationToken ct)
    {
        var usuarioId = User.ObtenerId() ?? throw new ApiException("La sesión no es válida.", StatusCodes.Status401Unauthorized);
        var turnoId = await turnos.SolicitarAsync(usuarioId, datos, ct);
        return CreatedAtAction(nameof(ObtenerPorId), new { id = turnoId }, await BuscarRespuestaAsync(turnoId, ct));
    }

    private async Task<TurnoRespuesta> BuscarRespuestaAsync(int id, CancellationToken ct) =>
        await Proyectar(ReservasVisibles().Where(r => r.TurnoId == id)).SingleOrDefaultAsync(ct)
        ?? throw new ApiException("No se encontró el turno.", StatusCodes.Status404NotFound);

    private IQueryable<CupoTurno> ReservasVisibles()
    {
        var reservas = db.CuposTurnos.AsNoTracking();
        if (User.IsInRole(Usuario.RolTutor))
        {
            var usuarioId = User.ObtenerId();
            reservas = reservas.Where(r => r.Turno.Perro.Tutor.UserId == usuarioId && !r.Turno.Perro.Tutor.Eliminado);
        }
        return reservas;
    }

    private IQueryable<TurnoRespuesta> Proyectar(IQueryable<CupoTurno> reservas) =>
        reservas.AsSplitQuery().Select(r => new TurnoRespuesta(r.TurnoId, r.Turno.PerroId, r.Turno.Perro.Nombre,
            r.CupoId, r.CupoDiario.FechaCupo, r.Cantidad, r.Turno.EstadoId, r.Turno.Estado.Descripcion, r.Turno.VeterinarioId,
            r.Turno.Observaciones, r.Turno.FechaSolicitud, r.Turno.FechaEstado,
            db.ServiciosPorTurno.Where(s => s.TurnoId == r.TurnoId).OrderBy(s => s.ServicioId)
                .Select(s => new ServicioTurnoRespuesta(s.ServicioId, s.Servicio.Descripcion, s.Completado)).ToList(),
            db.TratamientosPorTurno.Where(t => t.TurnoId == r.TurnoId).OrderBy(t => t.TratamientoId)
                .Select(t => new TratamientoTurnoRespuesta(t.TratamientoId, t.Tratamiento.Descripcion, t.Razon)).ToList()));
}
