using System.Globalization;
using turnero_peluqueria.Data;
using turnero_peluqueria.Entities;
using turnero_peluqueria.Security;

namespace turnero_peluqueria.Services;

internal sealed class AuditoriaAccionesService(AppDbContext db, IHttpContextAccessor contextoHttp)
{
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
