using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using turnero_peluqueria.Entities;

namespace turnero_peluqueria.Data.Configuraciones;

internal sealed class CupoDiarioConfiguracion : IEntityTypeConfiguration<CupoDiario>
{
    public void Configure(EntityTypeBuilder<CupoDiario> entidad)
    {
        entidad.ToTable("CuposDiarios", tabla => tabla.HasCheckConstraint("CK_CuposDiarios_Total", "[nro_cupo_dis] >= 0"));
        entidad.HasKey(x => x.Id);
        entidad.Property(x => x.Id).HasColumnName("id_cupo");
        entidad.Property(x => x.FechaCupo).HasColumnName("fecha_cupo").HasColumnType("date");
        entidad.Property(x => x.NroCupoDis).HasColumnName("nro_cupo_dis");
        entidad.HasIndex(x => x.FechaCupo).IsUnique();
    }
}

internal sealed class TurnoConfiguracion : IEntityTypeConfiguration<Turno>
{
    public void Configure(EntityTypeBuilder<Turno> entidad)
    {
        entidad.ToTable("Turnos", tabla =>
        {
            tabla.HasCheckConstraint("CK_Turnos_Fechas", "[fecha_estado] >= [fecha_solicitud]");
            tabla.HasCheckConstraint("CK_Turnos_Veterinario", "[estado_id] NOT IN (2, 5, 6) OR [veterinario_id] IS NOT NULL");
        });
        entidad.HasKey(x => x.Id);
        entidad.Property(x => x.Id).HasColumnName("id_turno");
        entidad.Property(x => x.PerroId).HasColumnName("perro_id");
        entidad.Property(x => x.EstadoId).HasColumnName("estado_id").HasDefaultValue(Estado.Pendiente);
        entidad.Property(x => x.VeterinarioId).HasColumnName("veterinario_id");
        entidad.Property(x => x.Observaciones).Texto("observaciones", 1000);
        entidad.Property(x => x.FechaSolicitud).HasColumnName("fecha_solicitud").HasColumnType("datetime").HasConversion(ConfiguracionModelo.FechaUtc).HasDefaultValueSql("GETUTCDATE()");
        entidad.Property(x => x.FechaEstado).HasColumnName("fecha_estado").HasColumnType("datetime").HasConversion(ConfiguracionModelo.FechaUtc).HasDefaultValueSql("GETUTCDATE()");
        entidad.HasIndex(x => x.FechaSolicitud);
        entidad.HasIndex(x => new { x.EstadoId, x.FechaEstado });
        entidad.HasOne(x => x.Perro).WithMany().HasForeignKey(x => x.PerroId).OnDelete(DeleteBehavior.NoAction);
        entidad.HasOne(x => x.Estado).WithMany().HasForeignKey(x => x.EstadoId).OnDelete(DeleteBehavior.NoAction);
        entidad.HasOne(x => x.Veterinario).WithMany().HasForeignKey(x => x.VeterinarioId).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class CupoTurnoConfiguracion : IEntityTypeConfiguration<CupoTurno>
{
    public void Configure(EntityTypeBuilder<CupoTurno> entidad)
    {
        entidad.ToTable("CuposTurnos", tabla => tabla.HasCheckConstraint("CK_CuposTurnos_Cantidad", "[cantidad] >= 1"));
        entidad.HasKey(x => new { x.CupoId, x.TurnoId });
        entidad.Property(x => x.CupoId).HasColumnName("cupo_id");
        entidad.Property(x => x.TurnoId).HasColumnName("turno_id");
        entidad.Property(x => x.Cantidad).HasColumnName("cantidad").HasDefaultValue(1);
        entidad.Property(x => x.Observacion).Texto("observacion", 500);
        entidad.HasIndex(x => x.TurnoId).IsUnique();
        entidad.HasOne(x => x.CupoDiario).WithMany().HasForeignKey(x => x.CupoId).OnDelete(DeleteBehavior.NoAction);
        entidad.HasOne(x => x.Turno).WithOne().HasForeignKey<CupoTurno>(x => x.TurnoId).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class TratamientoPorTurnoConfiguracion : IEntityTypeConfiguration<TratamientoPorTurno>
{
    public void Configure(EntityTypeBuilder<TratamientoPorTurno> entidad)
    {
        entidad.ToTable("TratamientosPorTurno");
        entidad.HasKey(x => new { x.TurnoId, x.TratamientoId });
        entidad.Property(x => x.TurnoId).HasColumnName("turno_id");
        entidad.Property(x => x.TratamientoId).HasColumnName("tratamiento_id");
        entidad.Property(x => x.Razon).Texto("razon", 500);
        entidad.HasOne(x => x.Turno).WithMany().HasForeignKey(x => x.TurnoId).OnDelete(DeleteBehavior.NoAction);
        entidad.HasOne(x => x.Tratamiento).WithMany().HasForeignKey(x => x.TratamientoId).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class ServicioPorTurnoConfiguracion : IEntityTypeConfiguration<ServicioPorTurno>
{
    public void Configure(EntityTypeBuilder<ServicioPorTurno> entidad)
    {
        entidad.ToTable("ServiciosPorTurno");
        entidad.HasKey(x => new { x.TurnoId, x.ServicioId });
        entidad.Property(x => x.TurnoId).HasColumnName("turno_id");
        entidad.Property(x => x.ServicioId).HasColumnName("servicio_id");
        entidad.Property(x => x.Completado).HasColumnName("completado").HasDefaultValue(false);
        entidad.HasOne(x => x.Turno).WithMany().HasForeignKey(x => x.TurnoId).OnDelete(DeleteBehavior.NoAction);
        entidad.HasOne(x => x.Servicio).WithMany().HasForeignKey(x => x.ServicioId).OnDelete(DeleteBehavior.NoAction);
    }
}
