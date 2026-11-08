using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace turnero_peluqueria.Data;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var entorno = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        var configuracion = new ConfigurationBuilder()
            .SetBasePath(Path.GetDirectoryName(typeof(AppDbContext).Assembly.Location)!)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{entorno}.json", optional: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();
        // Generar migraciones y scripts no requiere abrir una conexion.
        var conexion = configuracion.GetConnectionString("DefaultConnection")
            ?? "Server=localhost;Database=trabajo_final;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True";
        var opciones = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(conexion).Options;
        return new AppDbContext(opciones);
    }
}
