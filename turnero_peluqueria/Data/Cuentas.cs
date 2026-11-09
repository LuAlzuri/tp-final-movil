using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using turnero_peluqueria.DTOs;
using turnero_peluqueria.Entities;
using turnero_peluqueria.Exceptions;

namespace turnero_peluqueria.Data;

internal static class Cuentas
{
    public static async Task<int> ExigirVeterinarioAsync(AppDbContext db, int usuarioId, CancellationToken ct) =>
        await db.Veterinarios.Where(v => v.UserId == usuarioId && !v.Eliminado)
            .Select(v => (int?)v.Id).SingleOrDefaultAsync(ct)
        ?? throw new ApiException("No se encontró un perfil veterinario activo.", StatusCodes.Status403Forbidden);

    public static Usuario Crear(AltaCuentaSolicitud datos, string rol)
    {
        var usuario = new Usuario { Rol = rol };
        Actualizar(usuario, datos);
        usuario.PasswordHash = new PasswordHasher<Usuario>().HashPassword(usuario, datos.Contrasena);
        return usuario;
    }

    public static void Actualizar(Usuario usuario, DatosCuentaSolicitud datos)
    {
        usuario.Username = datos.Username.Trim();
        usuario.Email = datos.Email.Trim().ToLowerInvariant();
        usuario.Nombre = datos.Nombre.Trim();
        usuario.Apellido = datos.Apellido.Trim();
    }

    public static async Task VerificarDisponibilidad(AppDbContext db, DatosCuentaSolicitud datos, int? usuarioId, CancellationToken ct)
    {
        var username = datos.Username.Trim();
        var email = datos.Email.Trim().ToLowerInvariant();
        if (await db.Usuarios.AnyAsync(u => u.Id != usuarioId && (u.Username == username || u.Email == email), ct))
            throw new ApiException("El usuario o el correo ya está en uso.", StatusCodes.Status409Conflict);
    }

    public static async Task Guardar(AppDbContext db, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.GetBaseException() is SqlException { Number: 2601 or 2627 })
        {
            throw new ApiException("Alguno de los datos únicos ya está en uso.", StatusCodes.Status409Conflict);
        }
    }
}
