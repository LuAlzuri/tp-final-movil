using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using turnero_peluqueria.Data;
using turnero_peluqueria.Services;

namespace turnero_peluqueria.Security;

internal static class ConfiguracionAutenticacion
{
    public const string PoliticaInicioSesion = "inicio-sesion";

    public static void AgregarAutenticacion(this IServiceCollection servicios, IConfiguration configuracion)
    {
        servicios.AddHttpContextAccessor();
        servicios.AddSingleton(sp => JwtSettings.Cargar(sp.GetRequiredService<IConfiguration>()));
        servicios.AddScoped<AuditoriaAccionesService>();
        servicios.AddScoped(sp => new AutenticacionService(
            sp.GetRequiredService<AppDbContext>(),
            sp.GetRequiredService<JwtSettings>(),
            sp.GetRequiredService<AuditoriaAccionesService>()));

        servicios.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        servicios.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwtSettings>((opciones, jwt) =>
            {
                opciones.MapInboundClaims = false;
                opciones.IncludeErrorDetails = false;
                opciones.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = jwt.Clave,
                    RequireSignedTokens = true,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    NameClaimType = "name",
                    RoleClaimType = "role",
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
                opciones.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async contexto =>
                    {
                        var autenticacion = contexto.HttpContext.RequestServices.GetRequiredService<AutenticacionService>();
                        if (contexto.Principal is null || !await autenticacion.ValidarTokenAsync(
                            contexto.Principal, contexto.HttpContext.RequestAborted))
                        {
                            contexto.Fail("La sesión no es válida.");
                        }
                    },
                    OnChallenge = contexto =>
                    {
                        contexto.HandleResponse();
                        contexto.Response.Headers.WWWAuthenticate = "Bearer";
                        contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return contexto.Response.WriteAsJsonAsync(
                            new { error = "Se requiere una sesión válida." }, contexto.HttpContext.RequestAborted);
                    }
                };
            });

        servicios.AddAuthorization(opciones =>
        {
            opciones.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        });

        servicios.AddRateLimiter(opciones =>
        {
            var limite = configuracion.GetValue("LoginRateLimit:PermitLimit", 10);
            var segundos = configuracion.GetValue("LoginRateLimit:WindowSeconds", 60);
            if (limite is < 1 or > 1000 || segundos is < 1 or > 3600)
                throw new InvalidOperationException("Los valores de LoginRateLimit no son válidos.");

            opciones.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            opciones.AddPolicy(PoliticaInicioSesion, contexto =>
            {
                // Solo se usa la IP de la conexión, no cabeceras enviadas por el cliente.
                var ip = contexto.Connection.RemoteIpAddress;
                if (ip?.IsIPv4MappedToIPv6 == true)
                    ip = ip.MapToIPv4();
                return RateLimitPartition.GetFixedWindowLimiter(ip?.ToString() ?? "desconocida", _ => new()
                {
                    PermitLimit = limite,
                    Window = TimeSpan.FromSeconds(segundos),
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
            });
            opciones.OnRejected = async (contexto, ct) =>
            {
                var espera = contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var tiempo)
                    ? tiempo
                    : TimeSpan.FromSeconds(segundos);
                contexto.HttpContext.Response.Headers.RetryAfter =
                    Math.Max(1, Math.Ceiling(espera.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                await contexto.HttpContext.Response.WriteAsJsonAsync(
                    new { error = "Demasiados intentos de inicio de sesión. Volvé a intentarlo más tarde." }, ct);
            };
        });
    }
}
