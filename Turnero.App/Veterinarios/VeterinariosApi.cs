using System.Net.Http.Json;
using Turnero.App.Seguridad;

namespace Turnero.App.Veterinarios;

public sealed class VeterinariosApi(ApiGestion api)
{
    private const string Ruta = "api/veterinarios";
    public Task<List<Veterinario>> ListarAsync(CancellationToken ct = default) =>
        api.ListarPaginadoAsync<Veterinario>(Ruta, ct, recurso: "veterinarios");

    public async Task<Veterinario> ObtenerAsync(int id, CancellationToken ct = default)
    {
        using var respuesta = await api.EnviarAsync(HttpMethod.Get, $"{Ruta}/{id}", ct, recurso: "veterinarios");
        return await ApiGestion.LeerAsync<Veterinario>(respuesta, ct);
    }

    public async Task CrearAsync(DatosAltaVeterinario datos, CancellationToken ct = default)
    {
        using var contenido = JsonContent.Create(new
        {
            datos.Nombre, datos.Apellido, datos.Username, datos.Email, datos.Matricula, datos.Contrasena
        });
        using var respuesta = await api.EnviarAsync(HttpMethod.Post, Ruta, ct, contenido, recurso: "veterinarios");
    }

    public async Task ModificarAsync(int id, DatosVeterinario datos, CancellationToken ct = default)
    {
        using var contenido = JsonContent.Create(new
        {
            datos.Nombre, datos.Apellido, datos.Username, datos.Email, datos.Matricula
        });
        using var respuesta = await api.EnviarAsync(HttpMethod.Put, $"{Ruta}/{id}", ct, contenido, recurso: "veterinarios");
    }

    public async Task EliminarAsync(int id, CancellationToken ct = default)
    {
        using var respuesta = await api.EnviarAsync(HttpMethod.Delete, $"{Ruta}/{id}", ct, recurso: "veterinarios");
    }
}
