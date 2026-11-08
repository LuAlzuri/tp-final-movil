using Microsoft.EntityFrameworkCore;
using turnero_peluqueria.Entities;

namespace turnero_peluqueria.Data;

internal static class SemillasCatalogos
{
    public static void Configurar(ModelBuilder modelo)
    {
        modelo.Entity<Estado>().HasData(
            new Estado { Id = Estado.Pendiente, Descripcion = "Pendiente" },
            new Estado { Id = Estado.Aprobado, Descripcion = "Aprobado" },
            new Estado { Id = Estado.Rechazado, Descripcion = "Rechazado" },
            new Estado { Id = Estado.CanceladoPorTutor, Descripcion = "Cancelado por el tutor" },
            new Estado { Id = Estado.CanceladoPorVeterinario, Descripcion = "Cancelado por el veterinario" },
            new Estado { Id = Estado.Completado, Descripcion = "Completado" });

        modelo.Entity<GrupoEtario>().HasData(
            new GrupoEtario { Id = 1, Descripcion = "Cachorro" },
            new GrupoEtario { Id = 2, Descripcion = "Adulto" },
            new GrupoEtario { Id = 3, Descripcion = "Senior" });

        modelo.Entity<Tamanio>().HasData(
            new Tamanio { Id = 1, Descripcion = "Pequeño" },
            new Tamanio { Id = 2, Descripcion = "Mediano" },
            new Tamanio { Id = 3, Descripcion = "Grande" });
    }
}
