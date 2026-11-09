using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace turnero_peluqueria.Security;

internal sealed record JwtSettings(string Issuer, string Audience, int ExpireMinutes, SymmetricSecurityKey Clave)
{
    public static JwtSettings Cargar(IConfiguration configuracion)
    {
        var seccion = configuracion.GetSection("Jwt");
        var clave = seccion["Key"];
        var emisor = seccion["Issuer"];
        var audiencia = seccion["Audience"];
        var minutos = seccion.GetValue("ExpireMinutes", 60);

        if (string.IsNullOrWhiteSpace(clave) || Encoding.UTF8.GetByteCount(clave) < 32)
            throw new InvalidOperationException("Configure Jwt:Key con una clave secreta de al menos 32 bytes.");
        if (string.IsNullOrWhiteSpace(emisor) || string.IsNullOrWhiteSpace(audiencia))
            throw new InvalidOperationException("Configure Jwt:Issuer y Jwt:Audience.");
        if (minutos is < 1 or > 1440)
            throw new InvalidOperationException("Jwt:ExpireMinutes debe estar entre 1 y 1440.");

        return new JwtSettings(emisor, audiencia, minutos, new SymmetricSecurityKey(Encoding.UTF8.GetBytes(clave)));
    }
}
