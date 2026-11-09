using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using turnero_peluqueria.Data;
using turnero_peluqueria.DTOs;
using turnero_peluqueria.Entities;
using turnero_peluqueria.Exceptions;
using turnero_peluqueria.Security;
using turnero_peluqueria.Services;

namespace turnero_peluqueria.Controllers;

[ApiController]
[Route("api/tutores/{tutorId:int}/vetos")]
[Authorize(Roles = Usuario.RolVeterinario)]
public class VetosController(AppDbContext db, VetosService vetos) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<VetoRespuesta>>> Listar(int tutorId, CancellationToken ct,
        [Range(1, 1000000)] int pagina = 1, [Range(1, 100)] int tamano = 20)
    {
        if (!await db.Tutores.AnyAsync(t => t.Id == tutorId, ct))
            throw new ApiException("No se encontró el tutor.", StatusCodes.Status404NotFound);

        var historial = await db.Vetos.AsNoTracking().Where(v => v.TutorId == tutorId)
            .OrderByDescending(v => v.FechaInicio).ThenByDescending(v => v.Id)
            .Skip((pagina - 1) * tamano).Take(tamano).ToListAsync(ct);
        return historial.Select(Respuesta).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<VetoRespuesta>> ObtenerPorId(int tutorId, int id, CancellationToken ct)
    {
        var veto = await db.Vetos.AsNoTracking().SingleOrDefaultAsync(v => v.Id == id && v.TutorId == tutorId, ct)
            ?? throw new ApiException("No se encontró el veto del tutor.", StatusCodes.Status404NotFound);
        return Respuesta(veto);
    }

    [HttpPost]
    public async Task<ActionResult<VetoRespuesta>> Aplicar(int tutorId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] MotivoVetoSolicitud? datos, CancellationToken ct)
    {
        var veto = await vetos.AplicarAsync(tutorId, ExigirUsuarioId(), datos?.Motivo, ct);
        return CreatedAtAction(nameof(ObtenerPorId), new { tutorId, id = veto.Id }, Respuesta(veto));
    }

    [HttpPost("{id:int}/levantar")]
    public async Task<IActionResult> Levantar(int tutorId, int id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] MotivoVetoSolicitud? datos, CancellationToken ct)
    {
        await vetos.LevantarAsync(tutorId, id, ExigirUsuarioId(), datos?.Motivo, ct);
        return NoContent();
    }

    private int ExigirUsuarioId() => User.ObtenerId()
        ?? throw new ApiException("La sesión no es válida.", StatusCodes.Status401Unauthorized);

    private static VetoRespuesta Respuesta(Veto v) =>
        new(v.Id, v.TutorId, v.VeterinarioId, v.FechaInicio, v.Motivo, v.FechaFin, v.MotivoFinVeto);
}
