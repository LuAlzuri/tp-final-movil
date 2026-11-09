using System.ComponentModel.DataAnnotations;
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
[Route("api/tratamientos")]
[Authorize(Roles = Usuario.RolAdministrador + "," + Usuario.RolVeterinario)]
public class TratamientosController(AppDbContext db, AuditoriaAccionesService auditoria) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CatalogoRespuesta>>> Listar(
        CancellationToken ct, [Range(1, 1000000)] int pagina = 1, [Range(1, 100)] int tamano = 20) =>
        await db.Tratamientos.AsNoTracking().OrderBy(c => c.Id)
            .Skip((pagina - 1) * tamano).Take(tamano)
            .Select(c => new CatalogoRespuesta(c.Id, c.Descripcion)).ToListAsync(ct);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CatalogoRespuesta>> ObtenerPorId(int id, CancellationToken ct) =>
        await db.Tratamientos.AsNoTracking().Where(c => c.Id == id)
            .Select(c => new CatalogoRespuesta(c.Id, c.Descripcion)).SingleOrDefaultAsync(ct) ?? throw NoEncontrado();

    [HttpPost]
    public async Task<ActionResult<CatalogoRespuesta>> Crear(CatalogoSolicitud datos, CancellationToken ct)
    {
        var descripcion = datos.Descripcion.Trim();
        await VerificarDescripcion(descripcion, null, ct);
        var entrada = new Tratamiento { Descripcion = descripcion };
        db.Tratamientos.Add(entrada);
        await auditoria.GuardarAltaAsync(() => auditoria.Registrar("Crear", nameof(Tratamiento), entrada.Id), ct);
        return CreatedAtAction(nameof(ObtenerPorId), new { id = entrada.Id }, new CatalogoRespuesta(entrada.Id, entrada.Descripcion));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Modificar(int id, CatalogoSolicitud datos, CancellationToken ct)
    {
        var entrada = await db.Tratamientos.SingleOrDefaultAsync(c => c.Id == id, ct) ?? throw NoEncontrado();
        var descripcion = datos.Descripcion.Trim();
        await VerificarDescripcion(descripcion, id, ct);
        entrada.Descripcion = descripcion;
        auditoria.Registrar("Modificar", nameof(Tratamiento), id);
        await Cuentas.Guardar(db, ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        var entrada = await db.Tratamientos.SingleOrDefaultAsync(c => c.Id == id, ct) ?? throw NoEncontrado();
        if (await db.TratamientosPorTurno.AnyAsync(r => r.TratamientoId == id, ct))
            throw EnUso();

        db.Tratamientos.Remove(entrada);
        auditoria.Registrar("Eliminar", nameof(Tratamiento), id);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.GetBaseException() is SqlException { Number: 547 })
        {
            throw EnUso();
        }
        return NoContent();
    }

    private async Task VerificarDescripcion(string descripcion, int? id, CancellationToken ct)
    {
        if (await db.Tratamientos.AnyAsync(c => c.Id != id && c.Descripcion == descripcion, ct))
            throw new ApiException("La descripción ya está en uso.", StatusCodes.Status409Conflict);
    }

    private static ApiException NoEncontrado() =>
        new("No se encontró el tratamiento.", StatusCodes.Status404NotFound);

    private static ApiException EnUso() =>
        new("El tratamiento está asociado a un turno.", StatusCodes.Status409Conflict);
}
