using System.Globalization;
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
[Route("api/perros/{perroId:int}/patologias")]
[Authorize(Roles = Usuario.RolVeterinario)]
public class PerrosPatologiasController(AppDbContext db, AuditoriaAccionesService auditoria) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<PerroPatologiaRespuesta>> Asignar(int perroId, AsignarPatologiaSolicitud datos, CancellationToken ct)
    {
        if (!await db.Perros.AnyAsync(p => p.Id == perroId && !p.Eliminado, ct))
            throw new ApiException("No se encontró el perro.", StatusCodes.Status404NotFound);
        var patologia = await db.Patologias.AsNoTracking().SingleOrDefaultAsync(p => p.Id == datos.PatologiaId, ct)
            ?? throw new ApiException("No se encontró la patología.", StatusCodes.Status404NotFound);
        if (await db.PerrosPatologias.AnyAsync(r => r.PerroId == perroId && r.PatologiaId == datos.PatologiaId, ct))
            throw new ApiException("El perro ya tiene asignada esa patología.", StatusCodes.Status409Conflict);

        var relacion = new PerroPatologia { PerroId = perroId, PatologiaId = datos.PatologiaId };
        AplicarDatos(relacion, datos);
        db.PerrosPatologias.Add(relacion);
        Registrar("Crear", relacion);
        try
        {
            await Cuentas.Guardar(db, ct);
        }
        catch (DbUpdateException ex) when (ex.GetBaseException() is SqlException { Number: 547 })
        {
            throw new ApiException("La patología ya no está disponible.", StatusCodes.Status409Conflict);
        }

        return CreatedAtAction(nameof(PerrosController.ObtenerPorId), "Perros", new { id = perroId },
            new PerroPatologiaRespuesta(patologia.Id, patologia.Descripcion, relacion.Controlado, relacion.Observacion));
    }

    [HttpPut("{patologiaId:int}")]
    public async Task<IActionResult> Modificar(int perroId, int patologiaId, DatosPerroPatologiaSolicitud datos, CancellationToken ct)
    {
        var relacion = await BuscarAsignacionAsync(perroId, patologiaId, ct);
        AplicarDatos(relacion, datos);
        Registrar("Modificar", relacion);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{patologiaId:int}")]
    public async Task<IActionResult> Eliminar(int perroId, int patologiaId, CancellationToken ct)
    {
        var relacion = await BuscarAsignacionAsync(perroId, patologiaId, ct);
        db.PerrosPatologias.Remove(relacion);
        Registrar("Eliminar", relacion);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<PerroPatologia> BuscarAsignacionAsync(int perroId, int patologiaId, CancellationToken ct) =>
        await db.PerrosPatologias.SingleOrDefaultAsync(r => r.PerroId == perroId
            && r.PatologiaId == patologiaId && !r.Perro.Eliminado, ct)
        ?? throw new ApiException("No se encontró la patología asignada al perro.", StatusCodes.Status404NotFound);

    private static void AplicarDatos(PerroPatologia relacion, DatosPerroPatologiaSolicitud datos)
    {
        relacion.Controlado = datos.Controlado;
        relacion.Observacion = string.IsNullOrWhiteSpace(datos.Observacion) ? null : datos.Observacion.Trim();
    }

    private void Registrar(string accion, PerroPatologia relacion) =>
        auditoria.Registrar(accion, nameof(PerroPatologia),
            string.Create(CultureInfo.InvariantCulture, $"{relacion.PerroId}:{relacion.PatologiaId}"));
}
