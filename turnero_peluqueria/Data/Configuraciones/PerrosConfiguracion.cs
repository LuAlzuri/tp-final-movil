using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using turnero_peluqueria.Entities;

namespace turnero_peluqueria.Data.Configuraciones;

internal sealed class PerroConfiguracion : IEntityTypeConfiguration<Perro>
{
    public void Configure(EntityTypeBuilder<Perro> entidad)
    {
        entidad.ToTable("Perros", tabla => tabla.HasCheckConstraint("CK_Perros_Nombre", "LEN(LTRIM(RTRIM([nombre]))) > 0"));
        entidad.HasKey(x => x.Id);
        entidad.Property(x => x.Id).HasColumnName("id_perro");
        entidad.Property(x => x.TutorId).HasColumnName("tutor_id");
        entidad.Property(x => x.Nombre).Texto("nombre", 100).IsRequired();
        entidad.Property(x => x.FechaNacimiento).HasColumnName("fecha_nacimiento").HasColumnType("date");
        entidad.Property(x => x.Reactivo).HasColumnName("reactivo").HasDefaultValue(false);
        entidad.Property(x => x.CalVacCom).HasColumnName("cal_vac_com").HasDefaultValue(false);
        entidad.Property(x => x.Eliminado).HasColumnName("eliminado").HasDefaultValue(false);
        entidad.Property(x => x.GrupoEtarioId).HasColumnName("grupo_etario_id");
        entidad.Property(x => x.TamanioId).HasColumnName("tamanio_id");
        entidad.HasOne(x => x.Tutor).WithMany().HasForeignKey(x => x.TutorId).OnDelete(DeleteBehavior.NoAction);
        entidad.HasOne(x => x.GrupoEtario).WithMany().HasForeignKey(x => x.GrupoEtarioId).OnDelete(DeleteBehavior.NoAction);
        entidad.HasOne(x => x.Tamanio).WithMany().HasForeignKey(x => x.TamanioId).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class PerroPatologiaConfiguracion : IEntityTypeConfiguration<PerroPatologia>
{
    public void Configure(EntityTypeBuilder<PerroPatologia> entidad)
    {
        entidad.ToTable("PerrosPatologias");
        entidad.HasKey(x => new { x.PerroId, x.PatologiaId });
        entidad.Property(x => x.PerroId).HasColumnName("perro_id");
        entidad.Property(x => x.PatologiaId).HasColumnName("patologia_id");
        entidad.Property(x => x.Controlado).HasColumnName("controlado").HasDefaultValue(false);
        entidad.Property(x => x.Observacion).Texto("observacion", 1000);
        entidad.HasOne(x => x.Perro).WithMany().HasForeignKey(x => x.PerroId).OnDelete(DeleteBehavior.NoAction);
        entidad.HasOne(x => x.Patologia).WithMany().HasForeignKey(x => x.PatologiaId).OnDelete(DeleteBehavior.NoAction);
    }
}
