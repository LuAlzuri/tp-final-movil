using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net.Http.Json;
using Turnero.App.Seguridad;

namespace Turnero.App.Cupos;

internal sealed record CupoDiario(int Id, DateOnly FechaCupo, int CuposPublicados, int CuposOcupados, int CuposDisponibles)
{
    public string Dia => CultureInfo.GetCultureInfo("es-AR").DateTimeFormat.GetDayName(FechaCupo.DayOfWeek);
}

internal sealed class DatosCupo
{
    [Required(ErrorMessage = "Seleccioná la fecha.")]
    public DateOnly? FechaCupo { get; set; }

    [Required(ErrorMessage = "Ingresá la cantidad de cupos.")]
    [Range(0, int.MaxValue, ErrorMessage = "Ingresá una cantidad mayor o igual a 0.")]
    public int? CuposPublicados { get; set; }
}

internal sealed class CuposApi(ApiGestion api)
{
    private const string Ruta = "api/cupos-diarios";

    public Task<List<CupoDiario>> ListarAsync(CancellationToken ct) =>
        api.ListarPaginadoAsync<CupoDiario>(Ruta, ct, recurso: "cupos");

    public async Task<CupoDiario> ObtenerAsync(int id, CancellationToken ct)
    {
        using var respuesta = await api.EnviarAsync(HttpMethod.Get, $"{Ruta}/{id}", ct, recurso: "cupos");
        return await ApiGestion.LeerAsync<CupoDiario>(respuesta, ct);
    }

    public async Task GuardarAsync(int? id, DatosCupo datos, CancellationToken ct)
    {
        using var contenido = id.HasValue
            ? JsonContent.Create(new { datos.CuposPublicados })
            : JsonContent.Create(new { datos.FechaCupo, datos.CuposPublicados });
        using var respuesta = await api.EnviarAsync(id.HasValue ? HttpMethod.Put : HttpMethod.Post,
            id.HasValue ? $"{Ruta}/{id}" : Ruta, ct, contenido, recurso: "cupos");
    }

    public async Task EliminarAsync(int id, CancellationToken ct)
    {
        using var respuesta = await api.EnviarAsync(HttpMethod.Delete, $"{Ruta}/{id}", ct, recurso: "cupos");
    }
}