using System.Security.Claims;
using Turnero.App.Seguridad;

namespace Turnero.App.Components;

internal sealed record SeccionInicio(string Area, string Id, string Titulo)
{
    public string Ruta => $"{Area}/{Id}";
}

internal static class SeccionesInicio
{
    public const string RolesConMenu = Roles.Administrador + "," + Roles.Veterinario;
    private const string AreaAdministracion = "administracion";
    private const string AreaVeterinario = "veterinario";

    public static IReadOnlyList<SeccionInicio> Administracion { get; } =
    [
        new(AreaAdministracion, "veterinarios", "Gestionar veterinarios"),
        new(AreaAdministracion, "catalogos", "Gestionar catálogos"),
        new(AreaAdministracion, "auditoria", "Consultar auditoría"),
        new(AreaAdministracion, "informes", "Generar informes")
    ];

    public static IReadOnlyList<SeccionInicio> Veterinario { get; } =
    [
        new(AreaVeterinario, "turnos", "Turnos"),
        new(AreaVeterinario, "tutores", "Listado de tutores"),
        new(AreaVeterinario, "cupos", "Cupos"),
        new(AreaVeterinario, "catalogos", "Gestionar catálogos")
    ];

    public static string? AreaPara(ClaimsPrincipal usuario)
    {
        if (usuario.IsInRole(Roles.Administrador))
            return AreaAdministracion;

        return usuario.IsInRole(Roles.Veterinario) ? AreaVeterinario : null;
    }

    public static IReadOnlyList<SeccionInicio> Para(ClaimsPrincipal usuario) => AreaPara(usuario) switch
    {
        AreaAdministracion => Administracion,
        AreaVeterinario => Veterinario,
        _ => []
    };
}
