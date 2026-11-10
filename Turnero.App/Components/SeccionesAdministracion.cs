namespace Turnero.App.Components;

internal sealed record SeccionAdministrativa(string Id, string Titulo)
{
    public string Ruta => $"administracion/{Id}";
}

internal static class SeccionesAdministracion
{
    public static IReadOnlyList<SeccionAdministrativa> Todas { get; } =
    [
        new("veterinarios", "Gestionar veterinarios"),
        new("catalogos", "Gestionar catálogos"),
        new("auditoria", "Consultar auditoría"),
        new("informes", "Generar informes")
    ];
}
