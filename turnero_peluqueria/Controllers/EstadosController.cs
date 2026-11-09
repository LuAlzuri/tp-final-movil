using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using turnero_peluqueria.Data;
using turnero_peluqueria.DTOs;
using turnero_peluqueria.Entities;
using turnero_peluqueria.Exceptions;
using turnero_peluqueria.Services;

namespace turnero_peluqueria.Controllers;

[ApiController]
[Route("api/estados")]
[Authorize]
public class EstadosController(AppDbContext db, AuditoriaAccionesService auditoria) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CatalogoRespuesta>>> Listar(CancellationToken ct) =>
        await db.Estados.AsNoTracking().OrderBy(e => e.Id)
            .Select(e => new CatalogoRespuesta(e.Id, e.Descripcion)).ToListAsync(ct);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CatalogoRespuesta>> ObtenerPorId(int id, CancellationToken ct) =>
        await db.Estados.AsNoTracking().Where(e => e.Id == id)
            .Select(e => new CatalogoRespuesta(e.Id, e.Descripcion)).SingleOrDefaultAsync(ct) ?? throw NoEncontrado();

    [HttpPut("{id:int}")]
    [Authorize(Roles = Usuario.RolAdministrador)]
    public async Task<IActionResult> Modificar(int id, CatalogoSolicitud datos, CancellationToken ct)
    {
        var estado = await db.Estados.SingleOrDefaultAsync(e => e.Id == id, ct) ?? throw NoEncontrado();
        var descripcion = datos.Descripcion.Trim();
        if (await db.Estados.AnyAsync(e => e.Id != id && e.Descripcion == descripcion, ct))
            throw new ApiException("La descripción ya está en uso.", StatusCodes.Status409Conflict);

        estado.Descripcion = descripcion;
        auditoria.Registrar("Modificar", nameof(Estado), id);
        await Cuentas.Guardar(db, ct);
        return NoContent();
    }

    private static ApiException NoEncontrado() =>
        new("No se encontró el estado.", StatusCodes.Status404NotFound);
}
