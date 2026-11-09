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
[Route("api/tutores/{tutorId:int}/telefonos")]
[Authorize(Roles = Usuario.RolVeterinario)]
public class TelefonosTutoresController(AppDbContext db, AuditoriaAccionesService auditoria) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<ActionResult<TelefonoTutorRespuesta>> ObtenerPorId(int tutorId, int id, CancellationToken ct) =>
        await db.TelefonosTutores.AsNoTracking()
            .Where(t => t.TutorId == tutorId && t.Id == id && !t.Tutor.Eliminado)
            .Select(t => new TelefonoTutorRespuesta(t.Id, t.NombreProp, t.NroTel))
            .SingleOrDefaultAsync(ct) ?? throw NoEncontrado();

    [HttpPost]
    public async Task<ActionResult<TelefonoTutorRespuesta>> Crear(int tutorId, TelefonoTutorSolicitud datos, CancellationToken ct)
    {
        if (!await db.Tutores.AnyAsync(t => t.Id == tutorId && !t.Eliminado, ct))
            throw new ApiException("No se encontró el tutor.", StatusCodes.Status404NotFound);

        var numero = datos.NroTel.Trim();
        await VerificarNumero(tutorId, numero, null, ct);
        var telefono = new TelefonoTutor { TutorId = tutorId, NombreProp = datos.NombreProp.Trim(), NroTel = numero };
        db.TelefonosTutores.Add(telefono);
        await auditoria.GuardarAltaAsync(() => auditoria.Registrar("Crear", nameof(TelefonoTutor), telefono.Id), ct);
        return CreatedAtAction(nameof(ObtenerPorId), new { tutorId, id = telefono.Id },
            new TelefonoTutorRespuesta(telefono.Id, telefono.NombreProp, telefono.NroTel));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Modificar(int tutorId, int id, TelefonoTutorSolicitud datos, CancellationToken ct)
    {
        var telefono = await db.TelefonosTutores.SingleOrDefaultAsync(t => t.TutorId == tutorId && t.Id == id && !t.Tutor.Eliminado, ct)
            ?? throw NoEncontrado();
        var numero = datos.NroTel.Trim();
        await VerificarNumero(tutorId, numero, id, ct);
        telefono.NombreProp = datos.NombreProp.Trim();
        telefono.NroTel = numero;
        auditoria.Registrar("Modificar", nameof(TelefonoTutor), id);
        await Cuentas.Guardar(db, ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Eliminar(int tutorId, int id, CancellationToken ct)
    {
        var telefono = await db.TelefonosTutores.SingleOrDefaultAsync(
            t => t.TutorId == tutorId && t.Id == id && !t.Tutor.Eliminado, ct) ?? throw NoEncontrado();
        db.TelefonosTutores.Remove(telefono);
        auditoria.Registrar("Eliminar", nameof(TelefonoTutor), id);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task VerificarNumero(int tutorId, string numero, int? id, CancellationToken ct)
    {
        if (await db.TelefonosTutores.AnyAsync(t => t.TutorId == tutorId && t.Id != id && t.NroTel == numero, ct))
            throw new ApiException("El tutor ya tiene ese teléfono.", StatusCodes.Status409Conflict);
    }

    private static ApiException NoEncontrado() =>
        new("No se encontró el teléfono del tutor.", StatusCodes.Status404NotFound);
}
