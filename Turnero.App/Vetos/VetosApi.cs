using System.Net.Http.Json;
using Turnero.App.Seguridad;
using Turnero.App.Tutores;
using System.Collections.Concurrent;

namespace Turnero.App.Vetos;

internal sealed record Veto(int Id, int TutorId, int? VeterinarioId, DateTime FechaInicio,
    string? Motivo, DateTime? FechaFin, string? MotivoFinVeto, string? VeterinarioNombre = null);
internal sealed record EstadoVeto(Veto? Veto);

internal sealed class VetosApi(ApiGestion api)
{
    private static string Ruta(int tutorId) => $"api/tutores/{tutorId}/vetos";

    public async Task<List<Tutor>> FiltrarTutoresVetadosAsync(IReadOnlyCollection<Tutor> tutores, CancellationToken ct)
    {
        var vetados = new ConcurrentBag<Tutor>();
        await Parallel.ForEachAsync(tutores,
            new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct },
            async (tutor, token) =>
            {
                if (await VigenteAsync(tutor.Id, token) is not null)
                    vetados.Add(tutor);
            });
        return vetados.OrderBy(t => t.Apellido, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(t => t.Nombre, StringComparer.CurrentCultureIgnoreCase).ThenBy(t => t.Id).ToList();
    }

    public async Task<Veto?> VigenteAsync(int tutorId, CancellationToken ct)
    {
        using var respuesta = await api.EnviarAsync(HttpMethod.Get, $"{Ruta(tutorId)}/vigente", ct, recurso: "vetos");
        return (await ApiGestion.LeerAsync<EstadoVeto>(respuesta, ct)).Veto;
    }

    public async Task<List<Veto>> ListarAsync(int tutorId, int pagina, CancellationToken ct)
    {
        using var respuesta = await api.EnviarAsync(HttpMethod.Get, $"{Ruta(tutorId)}?pagina={pagina}&tamano=20", ct, recurso: "vetos");
        return await ApiGestion.LeerAsync<List<Veto>>(respuesta, ct);
    }

    public async Task GuardarAsync(int tutorId, int? vetoId, string? motivo, CancellationToken ct)
    {
        if (motivo?.Length > 500) throw new GestionApiException("El motivo admite hasta 500 caracteres.");
        using var contenido = JsonContent.Create(new { Motivo = motivo });
        using var respuesta = await api.EnviarAsync(HttpMethod.Post,
            vetoId is int id ? $"{Ruta(tutorId)}/{id}/levantar" : Ruta(tutorId), ct, contenido, "vetos");
    }
}
