using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using turnero_peluqueria.Entities;

namespace turnero_peluqueria.Data.Configuraciones;

internal sealed class UsuarioConfiguracion : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> entidad)
    {
        entidad.ToTable("Usuarios", tabla =>
        {
            tabla.HasCheckConstraint("CK_Usuarios_Rol", "[Rol] IN ('Administrador', 'Veterinario', 'Tutor')");
            tabla.HasCheckConstraint("CK_Usuarios_CamposObligatorios", "LEN(LTRIM(RTRIM([Username]))) > 0 AND LEN(LTRIM(RTRIM([Email]))) > 0 AND LEN(LTRIM(RTRIM([Nombre]))) > 0 AND LEN(LTRIM(RTRIM([Apellido]))) > 0 AND LEN([PasswordHash]) > 0");
        });
        entidad.HasKey(x => x.Id);
        entidad.Property(x => x.Username).Texto("Username", 100).UseCollation("Latin1_General_100_CI_AS").IsRequired();
        entidad.Property(x => x.Email).Texto("Email", 254).UseCollation("Latin1_General_100_CI_AS").IsRequired();
        entidad.Property(x => x.PasswordHash).Texto("PasswordHash", 512).IsRequired();
        entidad.Property(x => x.Nombre).Texto("Nombre", 100).IsRequired();
        entidad.Property(x => x.Apellido).Texto("Apellido", 100).IsRequired();
        entidad.Property(x => x.Rol).Texto("Rol", 20).UseCollation("Latin1_General_100_BIN2").IsRequired();
        entidad.HasIndex(x => x.Username).IsUnique();
        entidad.HasIndex(x => x.Email).IsUnique();
    }
}

internal sealed class VeterinarioConfiguracion : IEntityTypeConfiguration<Veterinario>
{
    public void Configure(EntityTypeBuilder<Veterinario> entidad)
    {
        entidad.ToTable("Veterinarios", tabla => tabla.HasCheckConstraint("CK_Veterinarios_Matricula", "[Matricula] BETWEEN 1 AND 99999"));
        entidad.HasKey(x => x.Id);
        entidad.Property(x => x.Id).HasColumnName("id_veterinario");
        entidad.Property(x => x.Eliminado).HasDefaultValue(false);
        entidad.HasIndex(x => x.Matricula).IsUnique();
        entidad.HasOne(x => x.Usuario).WithOne().HasForeignKey<Veterinario>(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class TutorConfiguracion : IEntityTypeConfiguration<Tutor>
{
    public void Configure(EntityTypeBuilder<Tutor> entidad)
    {
        entidad.ToTable("Tutores");
        entidad.HasKey(x => x.Id);
        entidad.Property(x => x.Id).HasColumnName("id_tutor");
        entidad.Property(x => x.Eliminado).HasDefaultValue(false);
        entidad.HasOne(x => x.Usuario).WithOne().HasForeignKey<Tutor>(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class TelefonoTutorConfiguracion : IEntityTypeConfiguration<TelefonoTutor>
{
    public void Configure(EntityTypeBuilder<TelefonoTutor> entidad)
    {
        entidad.ToTable("TelefonosTutores", tabla => tabla.HasCheckConstraint("CK_TelefonosTutores_CamposObligatorios", "LEN(LTRIM(RTRIM([nombre_prop]))) > 0 AND LEN(LTRIM(RTRIM([nro_tel]))) > 0"));
        entidad.HasKey(x => x.Id);
        entidad.Property(x => x.Id).HasColumnName("id_telefono");
        entidad.Property(x => x.TutorId).HasColumnName("tutor_id");
        entidad.Property(x => x.NombreProp).Texto("nombre_prop", 100).IsRequired();
        entidad.Property(x => x.NroTel).Texto("nro_tel", 20).IsRequired();
        entidad.HasIndex(x => new { x.TutorId, x.NroTel }).IsUnique();
        entidad.HasOne(x => x.Tutor).WithMany().HasForeignKey(x => x.TutorId).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class VetoConfiguracion : IEntityTypeConfiguration<Veto>
{
    public void Configure(EntityTypeBuilder<Veto> entidad)
    {
        entidad.ToTable("Vetos", tabla =>
        {
            tabla.HasCheckConstraint("CK_Vetos_Fechas", "[fecha_fin] IS NULL OR [fecha_fin] >= [fecha_inicio]");
            tabla.HasCheckConstraint("CK_Vetos_MotivoFin", "[fecha_fin] IS NOT NULL OR [motivo_fin_veto] IS NULL");
            tabla.HasCheckConstraint("CK_Vetos_Automatico", "[veterinario_id] IS NOT NULL OR ([motivo] IS NOT NULL AND [motivo] = 'Superó número de cancelaciones')");
        });
        entidad.HasKey(x => x.Id);
        entidad.Property(x => x.Id).HasColumnName("id_veto");
        entidad.Property(x => x.TutorId).HasColumnName("tutor_id");
        entidad.Property(x => x.VeterinarioId).HasColumnName("veterinario_id");
        entidad.Property(x => x.FechaInicio).HasColumnName("fecha_inicio").HasColumnType("datetime").HasConversion(ConfiguracionModelo.FechaUtc).HasDefaultValueSql("GETUTCDATE()");
        entidad.Property(x => x.FechaFin).HasColumnName("fecha_fin").HasColumnType("datetime").HasConversion(ConfiguracionModelo.FechaUtc);
        entidad.Property(x => x.Motivo).Texto("motivo", 500);
        entidad.Property(x => x.MotivoFinVeto).Texto("motivo_fin_veto", 500);
        entidad.HasIndex(x => x.TutorId).IsUnique().HasFilter("[fecha_fin] IS NULL");
        entidad.HasOne(x => x.Tutor).WithMany().HasForeignKey(x => x.TutorId).OnDelete(DeleteBehavior.NoAction);
        entidad.HasOne(x => x.Veterinario).WithMany().HasForeignKey(x => x.VeterinarioId).OnDelete(DeleteBehavior.NoAction);
    }
}
