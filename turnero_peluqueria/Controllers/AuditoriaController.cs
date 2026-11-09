using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using turnero_peluqueria.Data;
using turnero_peluqueria.DTOs;
using turnero_peluqueria.Entities;
using turnero_peluqueria.Services;

namespace turnero_peluqueria.Controllers;

[ApiController]
[Route("api/auditoria")]
[Authorize(Roles = Usuario.RolAdministrador)]
public class AuditoriaController(AppDbContext db, ConsultaErroresService errores) : ControllerBase
{
    [HttpGet("movimientos")]
    public async Task<ActionResult<List<MovimientoAuditoriaRespuesta>>> ListarMovimientos(
        CancellationToken ct, [Range(1, 1000000)] int pagina = 1, [Range(1, 100)] int tamano = 20,
        [StringLength(100)] string? entidad = null, [Range(1, int.MaxValue)] int? usuarioId = null)
    {
        var movimientos = db.AuditoriaAcciones.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(entidad))
            movimientos = movimientos.Where(m => m.Entidad == entidad.Trim());
        if (usuarioId.HasValue)
            movimientos = movimientos.Where(m => m.UsuarioId == usuarioId.Value);

        return await movimientos.OrderByDescending(m => m.FechaUtc).ThenByDescending(m => m.Id)
            .Skip((pagina - 1) * tamano).Take(tamano)
            .Select(m => new MovimientoAuditoriaRespuesta(m.Id, m.UsuarioId, m.UsuarioEmail, m.Accion,
                m.Entidad, m.EntidadId, m.FechaUtc, m.Ip, m.Observacion)).ToListAsync(ct);
    }

    [HttpGet("errores")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<List<ErrorLogRespuesta>>> ListarErrores(CancellationToken ct,
        DateOnly? desde = null, DateOnly? hasta = null, [Range(1, 1000000)] int pagina = 1,
        [Range(1, 100)] int tamano = 20) =>
        await errores.ListarAsync(desde, hasta, pagina, tamano, ct);
}
