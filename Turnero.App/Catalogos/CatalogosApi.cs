using System.Net.Http.Json;
using Turnero.App.Seguridad;

namespace Turnero.App.Catalogos;

internal sealed class CatalogosApi(ApiGestion api)
{
    public async Task<List<EntradaCatalogo>> ListarAsync(DefinicionCatalogo catalogo, CancellationToken ct)
    {
        if (catalogo.PermiteAltasYBajas)
            return await api.ListarPaginadoAsync<EntradaCatalogo>(catalogo.RutaApi, ct);

        using var respuesta = await api.EnviarAsync(HttpMethod.Get, catalogo.RutaApi, ct);
        return await ApiGestion.LeerAsync<List<EntradaCatalogo>>(respuesta, ct);
    }

    public async Task<EntradaCatalogo> ObtenerAsync(DefinicionCatalogo catalogo, int id, CancellationToken ct)
    {
        using var respuesta = await api.EnviarAsync(HttpMethod.Get, $"{catalogo.RutaApi}/{id}", ct);
        return await ApiGestion.LeerAsync<EntradaCatalogo>(respuesta, ct);
    }

    public async Task GuardarAsync(DefinicionCatalogo catalogo, int? id, DatosCatalogo datos, CancellationToken ct)
    {
        if (!id.HasValue && !catalogo.PermiteAltasYBajas)
            throw new GestionApiException("Este catálogo admite únicamente la edición de sus entradas.");

        var ruta = id.HasValue ? $"{catalogo.RutaApi}/{id}" : catalogo.RutaApi;
        using var contenido = JsonContent.Create(new { Descripcion = datos.Descripcion.Trim() });
        using var respuesta = await api.EnviarAsync(id.HasValue ? HttpMethod.Put : HttpMethod.Post, ruta, ct, contenido);
    }

    public async Task EliminarAsync(DefinicionCatalogo catalogo, int id, CancellationToken ct)
    {
        if (!catalogo.PermiteAltasYBajas)
            throw new GestionApiException("Este catálogo admite únicamente la edición de sus entradas.");

        using var respuesta = await api.EnviarAsync(HttpMethod.Delete, $"{catalogo.RutaApi}/{id}", ct);
    }
}
