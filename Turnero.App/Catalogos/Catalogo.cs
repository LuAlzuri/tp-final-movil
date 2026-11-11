using System.ComponentModel.DataAnnotations;

namespace Turnero.App.Catalogos;

internal sealed record DefinicionCatalogo(string Tipo, string Titulo, string Ejemplo)
{
    public bool PermiteAltasYBajas => Tipo != "estados";
    public string RutaApi => $"api/{Tipo}";
}

internal static class DefinicionesCatalogo
{
    public static IReadOnlyList<DefinicionCatalogo> Todas { get; } =
    [
        new("grupos-etarios", "Grupos etarios", "Adulto"),
        new("tamanios", "Tamaños", "Mediano"),
        new("patologias", "Patologías", "Alergia cutánea"),
        new("tratamientos", "Tratamientos", "Baño medicado"),
        new("servicios", "Servicios", "Corte higiénico"),
        new("estados", "Estados de turno", "Pendiente de aprobación")
    ];

    public static DefinicionCatalogo? Buscar(string? tipo) =>
        Todas.FirstOrDefault(catalogo => string.Equals(catalogo.Tipo, tipo, StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<DefinicionCatalogo> ParaVeterinario { get; } =
        Todas.Where(catalogo => catalogo.Tipo is "servicios" or "tratamientos" or "patologias").ToArray();
}

internal sealed record EntradaCatalogo(int Id, string Descripcion);

internal sealed class DatosCatalogo
{
    [Required(ErrorMessage = "Ingresá la descripción.")]
    [StringLength(100, ErrorMessage = "La descripción admite hasta 100 caracteres.")]
    public string Descripcion { get; set; } = string.Empty;
}
