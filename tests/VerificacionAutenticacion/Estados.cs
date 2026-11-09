using System.Net;
using System.Net.Http.Json;
using turnero_peluqueria.DTOs;
using turnero_peluqueria.Entities;

namespace turnero_peluqueria.Verificaciones;

internal static partial class VerificacionAutenticacion
{
    private static async Task VerificarEstadosAsync(HttpClient cliente, string tokenAdmin)
    {
        const string ruta = "/api/estados";
        var rutaCompletado = $"{ruta}/{Estado.Completado}";
        var datos = new CatalogoSolicitud { Descripcion = "Servicio finalizado" };

        Autorizar(cliente, null);
        await EsperarCodigo(cliente.GetAsync(ruta), HttpStatusCode.Unauthorized, "El catálogo requiere autenticación");
        var tutor = await IniciarSesionAsync(cliente, "tutor-servicios@turnero.test");
        Autorizar(cliente, tutor.Token);
        var iniciales = await cliente.GetFromJsonAsync<List<CatalogoRespuesta>>(ruta);
        Verificar(iniciales?.Select(e => e.Id).SequenceEqual(Enumerable.Range(1, 6)) == true, "El tutor puede consultar los seis estados");
        await EsperarCodigo(cliente.PutAsJsonAsync(rutaCompletado, datos), HttpStatusCode.Forbidden, "Solo el administrador puede editar estados");

        Autorizar(cliente, tokenAdmin);
        await EsperarCodigo(cliente.PostAsJsonAsync(ruta, datos), HttpStatusCode.MethodNotAllowed, "No se pueden agregar estados");
        await EsperarCodigo(cliente.DeleteAsync(rutaCompletado), HttpStatusCode.MethodNotAllowed, "No se pueden borrar estados");
        await EsperarCodigo(cliente.PutAsJsonAsync(ruta + "/7", datos), HttpStatusCode.NotFound, "Editar no debe crear un estado");
        await EsperarCodigo(cliente.PutAsJsonAsync(rutaCompletado, datos with { Descripcion = "Pendiente" }),
            HttpStatusCode.Conflict, "Las descripciones deben ser únicas");
        await EsperarCodigo(cliente.PutAsJsonAsync(rutaCompletado, datos), HttpStatusCode.NoContent, "Edición del estado");

        var editado = await cliente.GetFromJsonAsync<CatalogoRespuesta>(rutaCompletado);
        Verificar(editado == new CatalogoRespuesta(Estado.Completado, datos.Descripcion), "La edición conserva el identificador");
        var movimientos = await cliente.GetFromJsonAsync<List<MovimientoAuditoriaRespuesta>>("/api/auditoria/movimientos?entidad=Estado");
        Verificar(movimientos is [{ Accion: "Modificar", EntidadId: "6" }], "Solo la edición efectiva queda auditada");
    }
}
