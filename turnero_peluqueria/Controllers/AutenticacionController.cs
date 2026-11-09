using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using turnero_peluqueria.DTOs;
using turnero_peluqueria.Exceptions;
using turnero_peluqueria.Security;
using turnero_peluqueria.Services;

namespace turnero_peluqueria.Controllers;

[ApiController]
[Route("api/autenticacion")]
[Authorize]
public class AutenticacionController(AutenticacionService autenticacion) : ControllerBase
{
    [HttpPost("iniciar-sesion")]
    [AllowAnonymous]
    [EnableRateLimiting(ConfiguracionAutenticacion.PoliticaInicioSesion)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<SesionRespuesta>> IniciarSesion(IniciarSesionSolicitud solicitud, CancellationToken ct) =>
        Ok(await autenticacion.IniciarSesionAsync(solicitud, ct));

    [HttpGet("mi-perfil")]
    public async Task<ActionResult<UsuarioRespuesta>> ObtenerMiPerfil(CancellationToken ct) =>
        Ok(await autenticacion.ObtenerPerfilAsync(ExigirUsuarioId(), ct));

    [HttpPost("cambiar-contrasena")]
    public async Task<IActionResult> CambiarContrasena(CambiarContrasenaSolicitud solicitud, CancellationToken ct)
    {
        await autenticacion.CambiarContrasenaAsync(ExigirUsuarioId(), solicitud, ct);
        return NoContent();
    }

    private int ExigirUsuarioId() => User.ObtenerId()
        ?? throw new ApiException("La sesión no es válida.", StatusCodes.Status401Unauthorized);
}
