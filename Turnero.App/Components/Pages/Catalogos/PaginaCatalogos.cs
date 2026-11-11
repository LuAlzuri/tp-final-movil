using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Turnero.App.Catalogos;

namespace Turnero.App.Components.Pages.Catalogos;

public abstract class PaginaCatalogos : ComponentBase
{
    [Inject] protected NavigationManager Navegacion { get; set; } = default!;
    [CascadingParameter] private Task<AuthenticationState> EstadoAutenticacion { get; set; } = default!;

    protected string RutaCatalogos { get; private set; } = "inicio";
    internal IReadOnlyList<DefinicionCatalogo> CatalogosPermitidos { get; private set; } = [];

    protected async Task CargarAccesoAsync()
    {
        var usuario = (await EstadoAutenticacion).User;
        var area = SeccionesInicio.AreaPara(usuario);
        RutaCatalogos = area is null ? "inicio" : $"{area}/catalogos";
        var ruta = Navegacion.ToBaseRelativePath(Navegacion.Uri);
        var finArea = ruta.IndexOf('/');
        CatalogosPermitidos = finArea < 0 || !string.Equals(ruta[..finArea], area, StringComparison.OrdinalIgnoreCase)
            ? []
            : area == "veterinario" ? DefinicionesCatalogo.ParaVeterinario : DefinicionesCatalogo.Todas;
    }

    internal DefinicionCatalogo? BuscarCatalogo(string? tipo) =>
        CatalogosPermitidos.FirstOrDefault(catalogo => string.Equals(catalogo.Tipo, tipo, StringComparison.OrdinalIgnoreCase));
}
