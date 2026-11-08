using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using turnero_peluqueria.Entities;

namespace turnero_peluqueria.Data.Configuraciones;

internal sealed class AuditoriaAccionConfiguracion : IEntityTypeConfiguration<AuditoriaAccion>
{
    public void Configure(EntityTypeBuilder<AuditoriaAccion> entidad)
    {
        entidad.ToTable("AuditoriaAcciones", tabla => tabla.HasCheckConstraint("CK_AuditoriaAcciones_CamposObligatorios", "LEN(LTRIM(RTRIM([Accion]))) > 0 AND LEN(LTRIM(RTRIM([Entidad]))) > 0"));
        entidad.HasKey(x => x.Id);
        entidad.Property(x => x.UsuarioEmail).Texto("UsuarioEmail", 254);
        entidad.Property(x => x.Accion).Texto("Accion", 100).IsRequired();
        entidad.Property(x => x.Entidad).Texto("Entidad", 100).IsRequired();
        entidad.Property(x => x.EntidadId).Texto("EntidadId", 100);
        entidad.Property(x => x.FechaUtc).HasColumnType("datetime").HasConversion(ConfiguracionModelo.FechaUtc).HasDefaultValueSql("GETUTCDATE()");
        entidad.Property(x => x.Ip).Texto("Ip", 45);
        entidad.Property(x => x.Observacion).Texto("Observacion", 1000);
        entidad.HasIndex(x => new { x.FechaUtc, x.Id });
        entidad.HasIndex(x => new { x.Entidad, x.EntidadId });
        entidad.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.NoAction);
    }
}
