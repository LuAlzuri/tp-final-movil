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
[Route("api/tamanios")]
[Authorize(Roles = Usuario.RolAdministrador + "," + Usuario.RolVeterinario)]
public class TamaniosController(AppDbContext db, AuditoriaAccionesService auditoria) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CatalogoRespuesta>>> Listar(
        CancellationToken ct, [Range(1, 1000000)] int pagina = 1, [Range(1, 100)] int tamano = 20) =>
        await db.Tamanios.AsNoTracking().OrderBy(c => c.Id)
            .Skip((pagina - 1) * tamano).Take(tamano)
            .Select(c => new CatalogoRespuesta(c.Id, c.Descripcion)).ToListAsync(ct);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CatalogoRespuesta>> ObtenerPorId(int id, CancellationToken ct) =>
        await db.Tamanios.AsNoTracking().Where(c => c.Id == id)
            .Select(c => new CatalogoRespuesta(c.Id, c.Descripcion)).SingleOrDefaultAsync(ct) ?? throw NoEncontrado();

    [HttpPost]
    [Authorize(Roles = Usuario.RolAdministrador)]
    public async Task<ActionResult<CatalogoRespuesta>> Crear(CatalogoSolicitud datos, CancellationToken ct)
    {
        var descripcion = datos.Descripcion.Trim();
        await VerificarDescripcion(descripcion, null, ct);
        var entrada = new Tamanio { Descripcion = descripcion };
        db.Tamanios.Add(entrada);
        await auditoria.GuardarAltaAsync(() => auditoria.Registrar("Crear", nameof(Tamanio), entrada.Id), ct);
        return CreatedAtAction(nameof(ObtenerPorId), new { id = entrada.Id }, new CatalogoRespuesta(entrada.Id, entrada.Descripcion));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Usuario.RolAdministrador)]
    public async Task<IActionResult> Modificar(int id, CatalogoSolicitud datos, CancellationToken ct)
    {
        var entrada = await db.Tamanios.SingleOrDefaultAsync(c => c.Id == id, ct) ?? throw NoEncontrado();
        var descripcion = datos.Descripcion.Trim();
        await VerificarDescripcion(descripcion, id, ct);
        entrada.Descripcion = descripcion;
        auditoria.Registrar("Modificar", nameof(Tamanio), id);
        await Cuentas.Guardar(db, ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Usuario.RolAdministrador)]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        var entrada = await db.Tamanios.SingleOrDefaultAsync(c => c.Id == id, ct) ?? throw NoEncontrado();
        if (await db.Perros.AnyAsync(p => p.TamanioId == id, ct))
            throw EnUso();

        db.Tamanios.Remove(entrada);
        auditoria.Registrar("Eliminar", nameof(Tamanio), id);
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
        if (await db.Tamanios.AnyAsync(c => c.Id != id && c.Descripcion == descripcion, ct))
            throw new ApiException("La descripción ya está en uso.", StatusCodes.Status409Conflict);
    }

    private static ApiException NoEncontrado() =>
        new("No se encontró el tamaño.", StatusCodes.Status404NotFound);

    private static ApiException EnUso() =>
        new("El tamaño está asociado a un perro.", StatusCodes.Status409Conflict);
}
