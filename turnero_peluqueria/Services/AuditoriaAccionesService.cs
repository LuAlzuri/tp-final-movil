using System.Globalization;
using turnero_peluqueria.Data;
using turnero_peluqueria.Entities;
using turnero_peluqueria.Security;

namespace turnero_peluqueria.Services;

public sealed class AuditoriaAccionesService(AppDbContext db, IHttpContextAccessor contextoHttp)
{
    public async Task GuardarAltaAsync(Action registrarAlta, CancellationToken ct)
    {
        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        await Cuentas.Guardar(db, ct);
        registrarAlta();
        await db.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);
    }

    public void Registrar(string accion, string entidad, int entidadId, Usuario? autor = null)
    {
        var contexto = contextoHttp.HttpContext;
        db.AuditoriaAcciones.Add(new AuditoriaAccion
        {
            UsuarioId = autor?.Id ?? contexto?.User.ObtenerId(),
            UsuarioEmail = autor?.Email ?? contexto?.User.FindFirst("email")?.Value,
            Accion = accion,
            Entidad = entidad,
            EntidadId = entidadId.ToString(CultureInfo.InvariantCulture),
            Ip = contexto?.Connection.RemoteIpAddress?.ToString()
        });
    }
}
