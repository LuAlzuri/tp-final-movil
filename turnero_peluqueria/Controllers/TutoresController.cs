using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using turnero_peluqueria.Data;
using turnero_peluqueria.DTOs;
using turnero_peluqueria.Entities;
using turnero_peluqueria.Exceptions;

namespace turnero_peluqueria.Controllers;

[ApiController]
[Route("api/tutores")]
[Authorize(Roles = Usuario.RolVeterinario)]
public sealed class TutoresController(AppDbContext db) : ControllerBase
{
    private IQueryable<Tutor> Activos => db.Tutores.Where(t => !t.Eliminado);

    private IQueryable<TutorRespuesta> Proyectar(IQueryable<Tutor> tutores) =>
        tutores.Select(t => new TutorRespuesta(t.Id, t.UserId, t.Usuario.Username, t.Usuario.Email,
            t.Usuario.Nombre, t.Usuario.Apellido, db.TelefonosTutores.Where(f => f.TutorId == t.Id)
                .OrderBy(f => f.Id).Select(f => new TelefonoTutorRespuesta(f.Id, f.NombreProp, f.NroTel)).ToList()));

    [HttpGet]
    public async Task<ActionResult<List<TutorRespuesta>>> Listar(
        CancellationToken ct, [Range(1, 1000000)] int pagina = 1, [Range(1, 100)] int tamano = 20) =>
        await Proyectar(Activos.AsNoTracking().OrderBy(t => t.Id).Skip((pagina - 1) * tamano).Take(tamano)).ToListAsync(ct);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TutorRespuesta>> ObtenerPorId(int id, CancellationToken ct) =>
        await Proyectar(Activos.AsNoTracking().Where(t => t.Id == id)).SingleOrDefaultAsync(ct) ?? throw NoEncontrado();

    [HttpPost]
    public async Task<ActionResult<TutorRespuesta>> Crear(CrearTutorSolicitud datos, CancellationToken ct)
    {
        if (datos.Telefonos.Any(t => t is null))
            throw new ApiException("Cada teléfono debe incluir un nombre y un número.");

        var telefonos = datos.Telefonos.Select(t => new TelefonoTutor
        {
            NombreProp = t!.NombreProp.Trim(),
            NroTel = t.NroTel.Trim()
        }).ToList();
        if (telefonos.Select(t => t.NroTel).Distinct(StringComparer.OrdinalIgnoreCase).Count() != telefonos.Count)
            throw new ApiException("Los números de teléfono del tutor deben ser distintos.", StatusCodes.Status409Conflict);

        await Cuentas.VerificarDisponibilidad(db, datos, null, ct);
        var tutor = new Tutor { Usuario = Cuentas.Crear(datos, Usuario.RolTutor) };
        foreach (var telefono in telefonos)
            telefono.Tutor = tutor;

        db.Tutores.Add(tutor);
        db.TelefonosTutores.AddRange(telefonos);
        await Cuentas.Guardar(db, ct);

        var usuario = tutor.Usuario;
        var respuesta = new TutorRespuesta(tutor.Id, tutor.UserId, usuario.Username, usuario.Email, usuario.Nombre,
            usuario.Apellido, telefonos.Select(t => new TelefonoTutorRespuesta(t.Id, t.NombreProp, t.NroTel)).ToList());
        return CreatedAtAction(nameof(ObtenerPorId), new { id = tutor.Id }, respuesta);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Modificar(int id, DatosCuentaSolicitud datos, CancellationToken ct)
    {
        var tutor = await Activos.Include(t => t.Usuario).SingleOrDefaultAsync(t => t.Id == id, ct) ?? throw NoEncontrado();
        await Cuentas.VerificarDisponibilidad(db, datos, tutor.UserId, ct);
        Cuentas.Actualizar(tutor.Usuario, datos);
        await Cuentas.Guardar(db, ct);
        return NoContent();
    }

    private static ApiException NoEncontrado() =>
        new("No se encontró el tutor.", StatusCodes.Status404NotFound);
}
