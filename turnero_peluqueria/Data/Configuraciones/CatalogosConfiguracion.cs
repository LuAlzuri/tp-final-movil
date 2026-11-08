using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using turnero_peluqueria.Entities;

namespace turnero_peluqueria.Data.Configuraciones;

internal sealed class GrupoEtarioConfiguracion : IEntityTypeConfiguration<GrupoEtario>
{
    public void Configure(EntityTypeBuilder<GrupoEtario> entidad)
    {
        entidad.ToTable("GruposEtarios", tabla => tabla.HasCheckConstraint("CK_GruposEtarios_Descripcion", "LEN(LTRIM(RTRIM([grupo_descripcion]))) > 0"));
        entidad.HasKey(x => x.Id);
        entidad.Property(x => x.Id).HasColumnName("id_grupo_etario");
        entidad.Property(x => x.Descripcion).Texto("grupo_descripcion", 100).IsRequired();
        entidad.HasIndex(x => x.Descripcion).IsUnique();
    }
}

internal sealed class TamanioConfiguracion : IEntityTypeConfiguration<Tamanio>
{
    public void Configure(EntityTypeBuilder<Tamanio> entidad)
    {
        entidad.ToTable("Tamanios", tabla => tabla.HasCheckConstraint("CK_Tamanios_Descripcion", "LEN(LTRIM(RTRIM([tamanio_descripcion]))) > 0"));
        entidad.HasKey(x => x.Id);
        entidad.Property(x => x.Id).HasColumnName("id_tamanio");
        entidad.Property(x => x.Descripcion).Texto("tamanio_descripcion", 100).IsRequired();
        entidad.HasIndex(x => x.Descripcion).IsUnique();
    }
}

internal sealed class PatologiaConfiguracion : IEntityTypeConfiguration<Patologia>
{
    public void Configure(EntityTypeBuilder<Patologia> entidad)
    {
        entidad.ToTable("Patologias", tabla => tabla.HasCheckConstraint("CK_Patologias_Descripcion", "LEN(LTRIM(RTRIM([patologia_descripcion]))) > 0"));
        entidad.HasKey(x => x.Id);
        entidad.Property(x => x.Id).HasColumnName("id_patologia");
        entidad.Property(x => x.Descripcion).Texto("patologia_descripcion", 100).IsRequired();
        entidad.HasIndex(x => x.Descripcion).IsUnique();
    }
}

internal sealed class EstadoConfiguracion : IEntityTypeConfiguration<Estado>
{
    public void Configure(EntityTypeBuilder<Estado> entidad)
    {
        entidad.ToTable("Estados", tabla =>
        {
            tabla.HasCheckConstraint("CK_Estados_Identificador", "[id_estado] BETWEEN 1 AND 6");
            tabla.HasCheckConstraint("CK_Estados_Descripcion", "LEN(LTRIM(RTRIM([estado_descripcion]))) > 0");
        });
        entidad.HasKey(x => x.Id);
        entidad.Property(x => x.Id).HasColumnName("id_estado").UseIdentityColumn();
        entidad.Property(x => x.Descripcion).Texto("estado_descripcion", 100).IsRequired();
        entidad.HasIndex(x => x.Descripcion).IsUnique();
    }
}

internal sealed class TratamientoConfiguracion : IEntityTypeConfiguration<Tratamiento>
{
    public void Configure(EntityTypeBuilder<Tratamiento> entidad)
    {
        entidad.ToTable("Tratamientos", tabla => tabla.HasCheckConstraint("CK_Tratamientos_Descripcion", "LEN(LTRIM(RTRIM([tratamiento_descripcion]))) > 0"));
        entidad.HasKey(x => x.Id);
        entidad.Property(x => x.Id).HasColumnName("id_tratamiento");
        entidad.Property(x => x.Descripcion).Texto("tratamiento_descripcion", 100).IsRequired();
        entidad.HasIndex(x => x.Descripcion).IsUnique();
    }
}

internal sealed class ServicioConfiguracion : IEntityTypeConfiguration<Servicio>
{
    public void Configure(EntityTypeBuilder<Servicio> entidad)
    {
        entidad.ToTable("Servicios", tabla => tabla.HasCheckConstraint("CK_Servicios_Descripcion", "LEN(LTRIM(RTRIM([servicio_descripcion]))) > 0"));
        entidad.HasKey(x => x.Id);
        entidad.Property(x => x.Id).HasColumnName("id_servicio");
        entidad.Property(x => x.Descripcion).Texto("servicio_descripcion", 100).IsRequired();
        entidad.HasIndex(x => x.Descripcion).IsUnique();
    }
}
