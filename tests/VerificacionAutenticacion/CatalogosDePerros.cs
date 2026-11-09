using System.Net;
using System.Net.Http.Json;
using turnero_peluqueria.DTOs;
using turnero_peluqueria.Entities;

namespace turnero_peluqueria.Verificaciones;

internal static partial class VerificacionAutenticacion
{
    private static async Task VerificarCatalogosDePerrosAsync(HttpClient cliente, string tokenAdmin)
    {
        Autorizar(cliente, null);
        var veterinario = await IniciarSesionAsync(cliente, "vet-nuevo@turnero.test");
        foreach (var (ruta, entidad) in new[]
        {
            ("/api/grupos-etarios", nameof(GrupoEtario)),
            ("/api/tamanios", nameof(Tamanio))
        })
        {
            var datos = new CatalogoSolicitud { Descripcion = "  Prueba de catálogo  " };
            Autorizar(cliente, veterinario.Token);
            var semillas = await cliente.GetFromJsonAsync<List<CatalogoRespuesta>>(ruta);
            Verificar(semillas?.Count == 3, "El veterinario puede consultar el catálogo inicial");
            await EsperarCodigo(cliente.PostAsJsonAsync(ruta, datos), HttpStatusCode.Forbidden,
                "El veterinario no puede modificar este catálogo");

            Autorizar(cliente, tokenAdmin);
            using var alta = await cliente.PostAsJsonAsync(ruta, datos);
            Verificar(alta.StatusCode == HttpStatusCode.Created && alta.Headers.Location is not null, "Alta de catálogo");
            var entrada = (await alta.Content.ReadFromJsonAsync<CatalogoRespuesta>())!;
            Verificar(entrada.Descripcion == datos.Descripcion.Trim(), "La descripción se guarda sin espacios externos");
            await EsperarCodigo(cliente.PostAsJsonAsync(ruta, datos), HttpStatusCode.Conflict, "Descripción duplicada");
            await EsperarCodigo(cliente.PutAsJsonAsync(alta.Headers.Location, datos with { Descripcion = "Editada" }),
                HttpStatusCode.NoContent, "Edición de catálogo");
            await EsperarCodigo(cliente.DeleteAsync(ruta + "/2"), HttpStatusCode.Conflict, "El perro del historial impide eliminar la entrada");
            await EsperarCodigo(cliente.DeleteAsync(alta.Headers.Location), HttpStatusCode.NoContent, "Borrado de entrada libre");
            await EsperarCodigo(cliente.GetAsync(alta.Headers.Location), HttpStatusCode.NotFound, "La entrada se borró físicamente");

            var movimientos = await cliente.GetFromJsonAsync<List<MovimientoAuditoriaRespuesta>>(
                $"/api/auditoria/movimientos?entidad={entidad}");
            Verificar(movimientos?.Select(m => m.Accion).SequenceEqual(new[] { "Eliminar", "Modificar", "Crear" }) == true,
                "La auditoría incluye los cambios efectivos del catálogo");
        }
    }
}
