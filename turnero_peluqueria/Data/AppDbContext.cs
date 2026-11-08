using Microsoft.EntityFrameworkCore;
using turnero_peluqueria.Entities;

namespace turnero_peluqueria.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> opciones) : DbContext(opciones)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Veterinario> Veterinarios => Set<Veterinario>();
    public DbSet<Tutor> Tutores => Set<Tutor>();
    public DbSet<TelefonoTutor> TelefonosTutores => Set<TelefonoTutor>();
    public DbSet<Veto> Vetos => Set<Veto>();
    public DbSet<Perro> Perros => Set<Perro>();
    public DbSet<GrupoEtario> GruposEtarios => Set<GrupoEtario>();
    public DbSet<Tamanio> Tamanios => Set<Tamanio>();
    public DbSet<Patologia> Patologias => Set<Patologia>();
    public DbSet<PerroPatologia> PerrosPatologias => Set<PerroPatologia>();
    public DbSet<Estado> Estados => Set<Estado>();
    public DbSet<Tratamiento> Tratamientos => Set<Tratamiento>();
    public DbSet<Servicio> Servicios => Set<Servicio>();
    public DbSet<CupoDiario> CuposDiarios => Set<CupoDiario>();
    public DbSet<Turno> Turnos => Set<Turno>();
    public DbSet<CupoTurno> CuposTurnos => Set<CupoTurno>();
    public DbSet<TratamientoPorTurno> TratamientosPorTurno => Set<TratamientoPorTurno>();
    public DbSet<ServicioPorTurno> ServiciosPorTurno => Set<ServicioPorTurno>();
    public DbSet<AuditoriaAccion> AuditoriaAcciones => Set<AuditoriaAccion>();

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        base.OnModelCreating(modelo);
        modelo.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        SemillasCatalogos.Configurar(modelo);
    }
}
