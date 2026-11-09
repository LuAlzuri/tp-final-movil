using System.Globalization;
using System.Security.Claims;

namespace turnero_peluqueria.Security;

internal static class UsuarioAutenticado
{
    public static int? ObtenerId(this ClaimsPrincipal principal)
    {
        var identificadores = principal.FindAll("sub").Take(2).ToArray();
        return identificadores.Length == 1
            && int.TryParse(identificadores[0].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            && id > 0 ? id : null;
    }
}
