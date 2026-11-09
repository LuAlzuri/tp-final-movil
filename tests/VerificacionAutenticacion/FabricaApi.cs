using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using turnero_peluqueria.Data;

namespace turnero_peluqueria.Verificaciones;

internal sealed class FabricaApi(int limite = 100) : WebApplicationFactory<global::Program>
{
    public const string Emisor = "verificacion-turnero";
    public const string Audiencia = "verificacion-app";
    public string Clave { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuracion) =>
        {
            configuracion.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = Clave,
                ["Jwt:Issuer"] = Emisor,
                ["Jwt:Audience"] = Audiencia,
                ["Jwt:ExpireMinutes"] = "10",
                ["LoginRateLimit:PermitLimit"] = limite.ToString(CultureInfo.InvariantCulture),
                ["LoginRateLimit:WindowSeconds"] = "60",
                ["Logging:LogLevel:Default"] = "Warning",
                ["ErrorLog:Path"] = Path.Combine(Path.GetTempPath(), "turnero-verificacion-errores-.log")
            });
        });
        builder.ConfigureTestServices(servicios =>
        {
            servicios.Configure<KeyManagementOptions>(opciones => opciones.XmlRepository = new ClavesEnMemoria());
            servicios.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            servicios.RemoveAll<DbContextOptions<AppDbContext>>();
            servicios.RemoveAll<AppDbContext>();

            // La conexión solo existe mientras se ejecuta la prueba.
            servicios.AddSingleton(_ =>
            {
                var conexion = new SqliteConnection("Data Source=:memory:");
                conexion.Open();
                conexion.CreateCollation("Latin1_General_100_CI_AS", (a, b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase));
                conexion.CreateCollation("Latin1_General_100_BIN2", (a, b) => string.CompareOrdinal(a, b));
                conexion.CreateFunction("LEN", (string? texto) => texto?.TrimEnd(' ').Length, isDeterministic: true);
                conexion.CreateFunction("GETUTCDATE", () => DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                return conexion;
            });
            servicios.AddDbContext<AppDbContext>((sp, opciones) => opciones.UseSqlite(sp.GetRequiredService<SqliteConnection>()));
        });
    }

    public HttpClient CrearCliente() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false
    });

    private sealed class ClavesEnMemoria : IXmlRepository
    {
        private readonly ConcurrentQueue<XElement> elementos = new();

        public IReadOnlyCollection<XElement> GetAllElements() =>
            elementos.Select(elemento => new XElement(elemento)).ToArray();

        public void StoreElement(XElement element, string friendlyName) => elementos.Enqueue(new XElement(element));
    }
}
