using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace turnero_peluqueria.Data;

internal static class ConfiguracionModelo
{
    public static readonly ValueConverter<DateTime, DateTime> FechaUtc = new(
        valor => ExigirUtc(valor),
        valor => DateTime.SpecifyKind(valor, DateTimeKind.Utc));

    public static PropertyBuilder<T> Texto<T>(this PropertyBuilder<T> propiedad, string columna, int longitud) =>
        propiedad.HasColumnName(columna).HasMaxLength(longitud).IsUnicode(false);

    private static DateTime ExigirUtc(DateTime valor) => valor.Kind == DateTimeKind.Utc
        ? valor
        : throw new ArgumentException("Las fechas y horas deben expresarse en UTC.");
}
