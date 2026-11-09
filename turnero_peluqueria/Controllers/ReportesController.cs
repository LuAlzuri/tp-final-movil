using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using turnero_peluqueria.DTOs;
using turnero_peluqueria.Entities;
using turnero_peluqueria.Services;

namespace turnero_peluqueria.Controllers;

[ApiController]
[Route("api/reportes")]
[Authorize(Roles = Usuario.RolAdministrador)]
public class ReportesController(ReportesService reportes) : ControllerBase
{
    [HttpGet("frecuencia-solicitudes")]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK, "application/pdf")]
    public async Task<IActionResult> FrecuenciaSolicitudes([FromQuery] PeriodoReporteSolicitud periodo, CancellationToken ct) =>
        File(await reportes.FrecuenciaSolicitudesAsync(periodo, ct), "application/pdf", "frecuencia-solicitudes.pdf");

    [HttpGet("servicios-completos")]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK, "application/pdf")]
    public async Task<IActionResult> ServiciosCompletos([FromQuery] PeriodoReporteSolicitud periodo, CancellationToken ct) =>
        File(await reportes.ServiciosCompletosAsync(periodo, ct), "application/pdf", "servicios-completos.pdf");

    [HttpGet("servicios-incompletos")]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK, "application/pdf")]
    public async Task<IActionResult> ServiciosIncompletos([FromQuery] PeriodoReporteSolicitud periodo, CancellationToken ct) =>
        File(await reportes.ServiciosIncompletosAsync(periodo, ct), "application/pdf", "servicios-incompletos.pdf");
}
