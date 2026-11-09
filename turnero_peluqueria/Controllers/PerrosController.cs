using System.ComponentModel.DataAnnotations;
using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using turnero_peluqueria.Data;
using turnero_peluqueria.DTOs;
using turnero_peluqueria.Entities;
using turnero_peluqueria.Exceptions;
using turnero_peluqueria.Security;
using turnero_peluqueria.Services;

namespace turnero_peluqueria.Controllers;

[ApiController]
[Route("api/perros")]
[Authorize(Roles = Usuario.RolVeterinario + "," + Usuario.RolTutor)]
public class PerrosController(AppDbContext db, AuditoriaAccionesService auditoria, BajasService bajas,
    ImagenPerroService imagenes) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<PerroRespuesta>>> Listar(
        CancellationToken ct, [Range(1, 1000000)] int pagina = 1, [Range(1, 100)] int tamano = 20,
        [Range(1, int.MaxValue)] int? tutorId = null)
    {
        var perros = PerrosVisibles();
        if (tutorId.HasValue)
            perros = perros.Where(p => p.TutorId == tutorId.Value);

        return await Proyectar(perros.OrderBy(p => p.Id).Skip((pagina - 1) * tamano).Take(tamano)).ToListAsync(ct);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PerroRespuesta>> ObtenerPorId(int id, CancellationToken ct) =>
        await Proyectar(PerrosVisibles().Where(p => p.Id == id)).SingleOrDefaultAsync(ct) ?? throw NoEncontrado();

    [HttpPost]
    [Authorize(Roles = Usuario.RolVeterinario)]
    public async Task<ActionResult<PerroRespuesta>> Crear(CrearPerroSolicitud datos, CancellationToken ct)
    {
        try
        {
            await using var transaccion = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (!await db.Tutores.AnyAsync(t => t.Id == datos.TutorId && !t.Eliminado, ct))
                throw new ApiException("No se encontró un tutor activo con ese identificador.", StatusCodes.Status404NotFound);
            await ValidarDatosAsync(datos, ct);

            var perro = new Perro { TutorId = datos.TutorId };
            AplicarDatos(perro, datos);
            db.Perros.Add(perro);
            await db.SaveChangesAsync(ct);
            auditoria.Registrar("Crear", nameof(Perro), perro.Id);
            await db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);

            return CreatedAtAction(nameof(ObtenerPorId), new { id = perro.Id },
                new PerroRespuesta(perro.Id, perro.TutorId, perro.Nombre, perro.FechaNacimiento, perro.Reactivo,
                    perro.CalVacCom, perro.GrupoEtarioId, perro.TamanioId, [], null));
        }
        catch (Exception ex) when (ex.GetBaseException() is SqlException { Number: 1205 or 1222 or 3960 })
        {
            throw new ApiException("Hubo una modificación simultánea. Volvé a intentar el alta.", StatusCodes.Status409Conflict);
        }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Usuario.RolVeterinario)]
    public async Task<IActionResult> Modificar(int id, DatosPerroSolicitud datos, CancellationToken ct)
    {
        var perro = await db.Perros.SingleOrDefaultAsync(p => p.Id == id && !p.Eliminado, ct) ?? throw NoEncontrado();
        await ValidarDatosAsync(datos, ct);
        AplicarDatos(perro, datos);
        auditoria.Registrar("Modificar", nameof(Perro), id);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.GetBaseException() is SqlException { Number: 547 })
        {
            throw new ApiException("El grupo etario o el tamaño ya no está disponible.", StatusCodes.Status409Conflict);
        }
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Usuario.RolVeterinario)]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        await bajas.EliminarPerroAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/imagen")]
    [Authorize(Roles = Usuario.RolVeterinario)]
    [Consumes("multipart/form-data")]
    [LimiteCuerpoImagen]
    [RequestSizeLimit(ImagenPerroService.MaxPeticion)]
    [RequestFormLimits(MultipartBodyLengthLimit = ImagenPerroService.MaxPeticion)]
    public async Task<ActionResult<ImagenResponse>> SubirImagen(int id, [FromForm] ImagenSubidaRequest datos, CancellationToken ct) =>
        Ok(await imagenes.GuardarAsync(id, datos.Archivo, ct));

    [HttpDelete("{id:int}/imagen")]
    [Authorize(Roles = Usuario.RolVeterinario)]
    public async Task<IActionResult> EliminarImagen(int id, CancellationToken ct)
    {
        await imagenes.EliminarAsync(id, ct);
        return NoContent();
    }

    private IQueryable<Perro> PerrosVisibles()
    {
        var perros = db.Perros.AsNoTracking().Where(p => !p.Eliminado);
        if (User.IsInRole(Usuario.RolTutor))
        {
            var usuarioId = User.ObtenerId();
            perros = perros.Where(p => p.Tutor.UserId == usuarioId && !p.Tutor.Eliminado);
        }
        return perros;
    }

    private IQueryable<PerroRespuesta> Proyectar(IQueryable<Perro> perros) =>
        perros.Select(p => new PerroRespuesta(p.Id, p.TutorId, p.Nombre, p.FechaNacimiento, p.Reactivo, p.CalVacCom,
            p.GrupoEtarioId, p.TamanioId, db.PerrosPatologias.Where(r => r.PerroId == p.Id)
                .OrderBy(r => r.PatologiaId)
                .Select(r => new PerroPatologiaRespuesta(r.PatologiaId, r.Patologia.Descripcion, r.Controlado, r.Observacion))
                .ToList(), p.Imagen == null ? null : p.Imagen.RutaRelativa));

    private async Task ValidarDatosAsync(DatosPerroSolicitud datos, CancellationToken ct)
    {
        if (datos.FechaNacimiento == default || datos.FechaNacimiento > FechaArgentina.Hoy)
            throw new ApiException("La fecha de nacimiento es obligatoria y no puede ser futura.");
        if (!await db.GruposEtarios.AnyAsync(g => g.Id == datos.GrupoEtarioId, ct))
            throw new ApiException("El grupo etario indicado no existe.");
        if (!await db.Tamanios.AnyAsync(t => t.Id == datos.TamanioId, ct))
            throw new ApiException("El tamaño indicado no existe.");
    }

    private static void AplicarDatos(Perro perro, DatosPerroSolicitud datos)
    {
        perro.Nombre = datos.Nombre.Trim();
        perro.FechaNacimiento = datos.FechaNacimiento;
        perro.Reactivo = datos.Reactivo;
        perro.CalVacCom = datos.CalVacCom;
        perro.GrupoEtarioId = datos.GrupoEtarioId;
        perro.TamanioId = datos.TamanioId;
    }

    private static ApiException NoEncontrado() =>
        new("No se encontró el perro.", StatusCodes.Status404NotFound);
}
