using System.Net.Http.Json;
using Turnero.App.Seguridad;

namespace Turnero.App.Tutores;

internal sealed class TutoresApi(ApiGestion api)
{
    private const string Ruta = "api/tutores";

    public Task<List<Tutor>> ListarAsync(CancellationToken ct) =>
        api.ListarPaginadoAsync<Tutor>(Ruta, ct, recurso: "tutores");

    public async Task<Tutor> ObtenerAsync(int id, CancellationToken ct)
    {
        using var respuesta = await api.EnviarAsync(HttpMethod.Get, $"{Ruta}/{id}", ct, recurso: "tutores");
        return await ApiGestion.LeerAsync<Tutor>(respuesta, ct);
    }

    public async Task CrearAsync(DatosAltaTutor datos, CancellationToken ct)
    {
        using var contenido = JsonContent.Create(new
        {
            datos.Nombre, datos.Apellido, datos.Username, datos.Email, datos.Contrasena, datos.Telefonos
        });
        using var respuesta = await api.EnviarAsync(HttpMethod.Post, Ruta, ct, contenido, recurso: "tutores");
    }

    public async Task ModificarAsync(int id, DatosTutor datos, CancellationToken ct)
    {
        using var contenido = JsonContent.Create(new { datos.Nombre, datos.Apellido, datos.Username, datos.Email });
        using var respuesta = await api.EnviarAsync(HttpMethod.Put, $"{Ruta}/{id}", ct, contenido, recurso: "tutores");
    }

    public async Task EliminarAsync(int id, CancellationToken ct)
    {
        using var respuesta = await api.EnviarAsync(HttpMethod.Delete, $"{Ruta}/{id}", ct, recurso: "tutores");
    }

    public async Task<TelefonoTutor> ObtenerTelefonoAsync(int tutorId, int id, CancellationToken ct)
    {
        using var respuesta = await api.EnviarAsync(HttpMethod.Get, $"{Ruta}/{tutorId}/telefonos/{id}", ct, recurso: "telefonos");
        return await ApiGestion.LeerAsync<TelefonoTutor>(respuesta, ct);
    }

    public async Task GuardarTelefonoAsync(int tutorId, int? id, DatosTelefonoTutor datos, CancellationToken ct)
    {
        using var contenido = JsonContent.Create(datos);
        var ruta = $"{Ruta}/{tutorId}/telefonos";
        using var respuesta = id is int telefonoId
            ? await api.EnviarAsync(HttpMethod.Put, $"{ruta}/{telefonoId}", ct, contenido, recurso: "telefonos")
            : await api.EnviarAsync(HttpMethod.Post, ruta, ct, contenido, recurso: "telefonos");
    }

    public async Task EliminarTelefonoAsync(int tutorId, int id, CancellationToken ct)
    {
        using var respuesta = await api.EnviarAsync(HttpMethod.Delete, $"{Ruta}/{tutorId}/telefonos/{id}", ct, recurso: "telefonos");
    }
}
