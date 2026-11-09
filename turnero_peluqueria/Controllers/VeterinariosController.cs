using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
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
[Route("api/veterinarios")]
[Authorize(Roles = Usuario.RolAdministrador)]
public class VeterinariosController(AppDbContext db, AuditoriaAccionesService auditoria, BajasService bajas) : ControllerBase
{
    private static readonly Expression<Func<Veterinario, VeterinarioRespuesta>> Proyeccion =
        v => new(v.Id, v.UserId, v.Usuario.Username, v.Usuario.Email, v.Usuario.Nombre, v.Usuario.Apellido, v.Matricula);
    private static readonly Func<Veterinario, VeterinarioRespuesta> Mapear = Proyeccion.Compile();

    [HttpGet]
    public async Task<ActionResult<List<VeterinarioRespuesta>>> Listar(
        CancellationToken ct, [Range(1, 1000000)] int pagina = 1, [Range(1, 100)] int tamano = 20) =>
        await db.Veterinarios.AsNoTracking().Where(v => !v.Eliminado)
            .OrderBy(v => v.Id).Skip((pagina - 1) * tamano).Take(tamano).Select(Proyeccion).ToListAsync(ct);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<VeterinarioRespuesta>> ObtenerPorId(int id, CancellationToken ct) =>
        await db.Veterinarios.AsNoTracking().Where(v => v.Id == id && !v.Eliminado)
            .Select(Proyeccion).SingleOrDefaultAsync(ct) ?? throw NoEncontrado();

    [HttpPost]
    public async Task<ActionResult<VeterinarioRespuesta>> Crear(CrearVeterinarioSolicitud datos, CancellationToken ct)
    {
        await Cuentas.VerificarDisponibilidad(db, datos, null, ct);
        await VerificarMatricula(datos.Matricula, null, ct);
        var veterinario = new Veterinario
        {
            Usuario = Cuentas.Crear(datos, Usuario.RolVeterinario),
            Matricula = datos.Matricula
        };
        db.Veterinarios.Add(veterinario);
        await auditoria.GuardarAltaAsync(() => auditoria.Registrar("Crear", nameof(Veterinario), veterinario.Id), ct);
        return CreatedAtAction(nameof(ObtenerPorId), new { id = veterinario.Id }, Mapear(veterinario));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Modificar(int id, ModificarVeterinarioSolicitud datos, CancellationToken ct)
    {
        var veterinario = await db.Veterinarios.Include(v => v.Usuario)
            .SingleOrDefaultAsync(v => v.Id == id && !v.Eliminado, ct) ?? throw NoEncontrado();
        await Cuentas.VerificarDisponibilidad(db, datos, veterinario.UserId, ct);
        await VerificarMatricula(datos.Matricula, id, ct);
        Cuentas.Actualizar(veterinario.Usuario, datos);
        veterinario.Matricula = datos.Matricula;
        auditoria.Registrar("Modificar", nameof(Veterinario), id);
        await Cuentas.Guardar(db, ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        await bajas.EliminarVeterinarioAsync(id, ct);
        return NoContent();
    }

    private async Task VerificarMatricula(int matricula, int? id, CancellationToken ct)
    {
        if (await db.Veterinarios.AnyAsync(v => v.Id != id && v.Matricula == matricula, ct))
            throw new ApiException("La matrícula ya está en uso.", StatusCodes.Status409Conflict);
    }

    private static ApiException NoEncontrado() =>
        new("No se encontró el veterinario.", StatusCodes.Status404NotFound);
}
