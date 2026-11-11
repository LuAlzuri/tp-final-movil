using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Turnero.App.Seguridad;

namespace Turnero.App.Perros;

internal sealed record Perro(int Id, int TutorId, string Nombre, DateOnly FechaNacimiento,
    bool Reactivo, bool CalVacCom, int GrupoEtarioId, int TamanioId, List<PatologiaPerro> Patologias,
    string? ImagenUrl = null);

public sealed record PatologiaPerro(string Descripcion, bool Controlado, string? Observacion, int PatologiaId = 0);

internal sealed class DatosPerro
{
    [Required(ErrorMessage = "Ingresá el nombre.")]
    [StringLength(100)]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ingresá la fecha de nacimiento.")]
    public DateOnly? FechaNacimiento { get; set; }

    public bool Reactivo { get; set; }
    public bool CalVacCom { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná el grupo etario.")]
    public int GrupoEtarioId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Seleccioná el tamaño.")]
    public int TamanioId { get; set; }
}

internal sealed class PerrosApi(ApiGestion api, HttpClient cliente)
{
    private const string Ruta = "api/perros";
    public const int MaximoImagen = 5 * 1024 * 1024;

    public string? ResolverImagen(string? ruta) =>
        ruta is not null && ruta.StartsWith("/uploads/", StringComparison.Ordinal)
            ? new Uri(cliente.BaseAddress!, ruta).AbsoluteUri
            : null;

    public Task<List<Perro>> ListarAsync(int tutorId, CancellationToken ct) =>
        api.ListarPaginadoAsync<Perro>($"{Ruta}?tutorId={tutorId}", ct, recurso: "perros");

    public Task<List<Perro>> ListarPropiosAsync(CancellationToken ct) =>
        api.ListarPaginadoAsync<Perro>(Ruta, ct, recurso: "perros");

    public async Task<Perro> ObtenerAsync(int id, CancellationToken ct)
    {
        using var respuesta = await api.EnviarAsync(HttpMethod.Get, $"{Ruta}/{id}", ct, recurso: "perros");
        return await ApiGestion.LeerAsync<Perro>(respuesta, ct);
    }

    public async Task<int> GuardarAsync(int tutorId, int? id, DatosPerro datos, CancellationToken ct)
    {
        using var contenido = JsonContent.Create(new
        {
            TutorId = tutorId,
            Nombre = datos.Nombre.Trim(),
            datos.FechaNacimiento,
            datos.Reactivo,
            datos.CalVacCom,
            datos.GrupoEtarioId,
            datos.TamanioId
        });
        using var respuesta = await api.EnviarAsync(id.HasValue ? HttpMethod.Put : HttpMethod.Post,
            id.HasValue ? $"{Ruta}/{id}" : Ruta, ct, contenido, recurso: "perros");
        return id ?? (await ApiGestion.LeerAsync<Perro>(respuesta, ct)).Id;
    }

    public async Task SubirImagenAsync(int id, byte[] imagen, string nombre, string tipo, CancellationToken ct)
    {
        using var archivo = new ByteArrayContent(imagen);
        archivo.Headers.ContentType = new MediaTypeHeaderValue(tipo);
        using var contenido = new MultipartFormDataContent();
        contenido.Add(archivo, "archivo", nombre);
        using var respuesta = await api.EnviarAsync(HttpMethod.Post, $"{Ruta}/{id}/imagen", ct, contenido, recurso: "perros");
    }

    public async Task EliminarAsync(int id, CancellationToken ct)
    {
        using var respuesta = await api.EnviarAsync(HttpMethod.Delete, $"{Ruta}/{id}", ct, recurso: "perros");
    }

    public async Task GuardarPatologiasAsync(int id, List<PatologiaPerro> originales,
        IReadOnlyList<PatologiaPerro> seleccionadas, CancellationToken ct)
    {
        var eliminadas = originales.Where(anterior => seleccionadas.All(actual => actual.PatologiaId != anterior.PatologiaId)).ToArray();
        var cambios = seleccionadas.Where(actual => !originales.Contains(actual)).ToArray();
        if (eliminadas.Length == 0 && cambios.Length == 0)
            return;

        var registradas = (await ObtenerAsync(id, ct)).Patologias.ToDictionary(patologia => patologia.PatologiaId);
        var ruta = $"{Ruta}/{id}/patologias";
        foreach (var eliminada in eliminadas)
        {
            if (registradas.ContainsKey(eliminada.PatologiaId))
            {
                using var respuesta = await api.EnviarAsync(HttpMethod.Delete, $"{ruta}/{eliminada.PatologiaId}", ct, recurso: "perros");
            }
            originales.Remove(eliminada);
        }

        foreach (var cambio in cambios)
        {
            var existe = registradas.TryGetValue(cambio.PatologiaId, out var actual);
            if (actual is null || actual.Controlado != cambio.Controlado || actual.Observacion != cambio.Observacion)
            {
                using var contenido = JsonContent.Create(new { cambio.PatologiaId, cambio.Controlado, cambio.Observacion });
                using var respuesta = await api.EnviarAsync(existe ? HttpMethod.Put : HttpMethod.Post,
                    existe ? $"{ruta}/{cambio.PatologiaId}" : ruta, ct, contenido, recurso: "perros");
            }
            originales.RemoveAll(patologia => patologia.PatologiaId == cambio.PatologiaId);
            originales.Add(cambio);
        }
    }
}
