using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using turnero_peluqueria.Data;
using turnero_peluqueria.DTOs;
using turnero_peluqueria.Entities;
using turnero_peluqueria.Exceptions;
using turnero_peluqueria.Security;

namespace turnero_peluqueria.Services;

public sealed class AutenticacionService
{
    private readonly AppDbContext db;
    private readonly JwtSettings jwt;
    private readonly AuditoriaAccionesService auditoria;
    private readonly PasswordHasher<Usuario> hasher = new();

    // Realiza una verificación de costo similar cuando el correo no existe.
    private static readonly string HashAuxiliar = new PasswordHasher<Usuario>()
        .HashPassword(new Usuario(), Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));

    internal AutenticacionService(AppDbContext db, JwtSettings jwt, AuditoriaAccionesService auditoria)
    {
        this.db = db;
        this.jwt = jwt;
        this.auditoria = auditoria;
    }

    private IQueryable<Cuenta> Cuentas(IQueryable<Usuario> usuarios) =>
        from usuario in usuarios.AsNoTracking()
        join veterinario in db.Veterinarios on usuario.Id equals veterinario.UserId into veterinarios
        from veterinario in veterinarios.DefaultIfEmpty()
        join tutor in db.Tutores on usuario.Id equals tutor.UserId into tutores
        from tutor in tutores.DefaultIfEmpty()
        select new Cuenta(
            usuario,
            veterinario == null ? null : veterinario.Id,
            veterinario == null ? null : veterinario.Eliminado,
            tutor == null ? null : tutor.Id,
            tutor == null ? null : tutor.Eliminado);

    public async Task<SesionRespuesta> IniciarSesionAsync(IniciarSesionSolicitud solicitud, CancellationToken ct)
    {
        var correo = NormalizarCorreo(solicitud.Email);
        var cuenta = await Cuentas(db.Usuarios.Where(x => x.Email == correo)).SingleOrDefaultAsync(ct);
        var usuario = cuenta?.Usuario ?? new Usuario();
        var resultado = hasher.VerifyHashedPassword(usuario, cuenta?.Usuario.PasswordHash ?? HashAuxiliar, solicitud.Contrasena);

        if (cuenta is null || !cuenta.Activa || resultado == PasswordVerificationResult.Failed)
            throw CredencialesInvalidas();

        if (resultado == PasswordVerificationResult.SuccessRehashNeeded)
        {
            var hash = hasher.HashPassword(usuario, solicitud.Contrasena);
            if (await ActualizarHashAsync(usuario, hash, ct) != 1)
                throw CredencialesInvalidas();
            usuario.PasswordHash = hash;
        }

        var ahora = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).UtcDateTime;
        var expira = ahora.AddMinutes(jwt.ExpireMinutes);
        Claim[] claims =
        [
            new("sub", usuario.Id.ToString(CultureInfo.InvariantCulture)),
            new("name", $"{usuario.Nombre} {usuario.Apellido}"),
            new("email", usuario.Email),
            new("role", usuario.Rol),
            new("auth_version", VersionCredenciales(usuario)),
            new("jti", Guid.NewGuid().ToString("N"))
        ];
        var token = new JwtSecurityToken(jwt.Issuer, jwt.Audience, claims, ahora, expira,
            new SigningCredentials(jwt.Clave, SecurityAlgorithms.HmacSha256));

        return new SesionRespuesta(new JwtSecurityTokenHandler().WriteToken(token), expira, cuenta.Respuesta());
    }

    internal async Task<bool> ValidarTokenAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        if (principal.ObtenerId() is not int id)
            return false;

        var roles = principal.FindAll("role").Take(2).ToArray();
        var versiones = principal.FindAll("auth_version").Take(2).ToArray();
        if (roles.Length != 1 || versiones.Length != 1)
            return false;

        var cuenta = await Cuentas(db.Usuarios.Where(x => x.Id == id)).SingleOrDefaultAsync(ct);
        return cuenta is { Activa: true }
            && cuenta.Usuario.Rol == roles[0].Value
            && VersionCredenciales(cuenta.Usuario) == versiones[0].Value;
    }

    public async Task<UsuarioRespuesta> ObtenerPerfilAsync(int id, CancellationToken ct) =>
        (await ExigirCuentaActivaAsync(id, ct)).Respuesta();

    public async Task CambiarContrasenaAsync(int id, CambiarContrasenaSolicitud solicitud, CancellationToken ct)
    {
        try
        {
            await using var transaccion = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var usuario = (await ExigirCuentaActivaAsync(id, ct)).Usuario;

            if (hasher.VerifyHashedPassword(usuario, usuario.PasswordHash, solicitud.ContrasenaActual)
                == PasswordVerificationResult.Failed)
                throw new ApiException("La contraseña actual no es correcta.", StatusCodes.Status403Forbidden);

            if (solicitud.ContrasenaNueva == solicitud.ContrasenaActual)
                throw new ApiException("La contraseña nueva debe ser distinta de la actual.");

            var hash = hasher.HashPassword(usuario, solicitud.ContrasenaNueva);
            if (await ActualizarHashAsync(usuario, hash, ct) != 1)
                throw new ApiException("La cuenta cambió. Volvé a iniciar sesión.", StatusCodes.Status409Conflict);

            auditoria.Registrar("CambiarContrasena", nameof(Usuario), usuario.Id, usuario);
            await db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);
        }
        catch (Exception ex) when (EsConflictoSql(ex))
        {
            throw new ApiException("La cuenta cambió durante la operación. Volvé a intentarlo.", StatusCodes.Status409Conflict);
        }
    }

    public async Task CrearAdministradorInicialAsync(CrearAdministradorSolicitud solicitud, CancellationToken ct)
    {
        if (!Validator.TryValidateObject(solicitud, new ValidationContext(solicitud), null, validateAllProperties: true))
            throw new ApiException("Revisá los datos de Bootstrap. La contraseña debe tener entre 12 y 128 caracteres.");

        var correo = NormalizarCorreo(solicitud.Email);
        var username = solicitud.Username.Trim();

        try
        {
            await using var transaccion = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (await db.Usuarios.AnyAsync(u => u.Rol == Usuario.RolAdministrador, ct))
                throw new ApiException("Ya existe un administrador.", StatusCodes.Status409Conflict);
            if (await db.Usuarios.AnyAsync(u => u.Email == correo || u.Username == username, ct))
                throw new ApiException("El correo o el nombre de usuario ya está en uso.", StatusCodes.Status409Conflict);

            var usuario = new Usuario
            {
                Username = username,
                Nombre = solicitud.Nombre.Trim(),
                Apellido = solicitud.Apellido.Trim(),
                Email = correo,
                Rol = Usuario.RolAdministrador
            };
            usuario.PasswordHash = hasher.HashPassword(usuario, solicitud.Contrasena);
            db.Usuarios.Add(usuario);
            await db.SaveChangesAsync(ct);

            auditoria.Registrar("CrearAdministradorInicial", nameof(Usuario), usuario.Id, usuario);
            await db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);
        }
        catch (Exception ex) when (EsConflictoSql(ex))
        {
            throw new ApiException("No se pudo crear el administrador porque los datos cambiaron.", StatusCodes.Status409Conflict);
        }
    }

    private async Task<Cuenta> ExigirCuentaActivaAsync(int id, CancellationToken ct)
    {
        var cuenta = await Cuentas(db.Usuarios.Where(x => x.Id == id)).SingleOrDefaultAsync(ct);
        return cuenta is { Activa: true } ? cuenta : throw CredencialesInvalidas();
    }

    private Task<int> ActualizarHashAsync(Usuario usuario, string hashNuevo, CancellationToken ct) =>
        db.Usuarios.Where(u => u.Id == usuario.Id
            && EF.Functions.Collate(u.PasswordHash, "Latin1_General_100_BIN2") == usuario.PasswordHash)
            .ExecuteUpdateAsync(cambios => cambios.SetProperty(u => u.PasswordHash, hashNuevo), ct);

    private static bool EsConflictoSql(Exception ex) =>
        ex.GetBaseException() is SqlException { Number: 1205 or 1222 or 2601 or 2627 or 3960 };

    private static string NormalizarCorreo(string correo) => correo.Trim().ToLowerInvariant();

    private static string VersionCredenciales(Usuario usuario) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(usuario.PasswordHash)));

    private static ApiException CredencialesInvalidas() =>
        new("Credenciales inválidas o cuenta no disponible.", StatusCodes.Status401Unauthorized);

    private sealed record Cuenta(
        Usuario Usuario, int? VeterinarioId, bool? VeterinarioEliminado, int? TutorId, bool? TutorEliminado)
    {
        public bool Activa => Usuario.Rol switch
        {
            Usuario.RolAdministrador => VeterinarioId is null && TutorId is null,
            Usuario.RolVeterinario => VeterinarioId is not null && VeterinarioEliminado == false && TutorId is null,
            Usuario.RolTutor => TutorId is not null && TutorEliminado == false && VeterinarioId is null,
            _ => false
        };

        public UsuarioRespuesta Respuesta() => new(
            Usuario.Id, Usuario.Username, Usuario.Email, Usuario.Nombre, Usuario.Apellido, Usuario.Rol,
            VeterinarioId, TutorId);
    }
}
