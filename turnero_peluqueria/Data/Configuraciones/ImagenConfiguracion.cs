using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using turnero_peluqueria.Entities;

namespace turnero_peluqueria.Data.Configuraciones;

internal sealed class ImagenConfiguracion : IEntityTypeConfiguration<Imagen>
{
    public void Configure(EntityTypeBuilder<Imagen> entidad)
    {
        entidad.ToTable("Imagenes", tabla => tabla.HasCheckConstraint("CK_Imagenes_Tamano", "[tamano_bytes] > 0"));
        entidad.HasKey(i => i.Id);
        entidad.Property(i => i.Id).HasColumnName("id_imagen");
        entidad.Property(i => i.PerroId).HasColumnName("perro_id");
        entidad.Property(i => i.NombreArchivo).Texto("nombre_archivo", 255).IsRequired();
        entidad.Property(i => i.RutaRelativa).Texto("ruta_relativa", 500).IsRequired();
        entidad.Property(i => i.TipoContenido).Texto("tipo_contenido", 100).IsRequired();
        entidad.Property(i => i.TamanoBytes).HasColumnName("tamano_bytes");
        entidad.HasOne(i => i.Perro).WithOne(p => p.Imagen).HasForeignKey<Imagen>(i => i.PerroId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
