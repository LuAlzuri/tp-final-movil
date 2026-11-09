using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Serilog;
using Serilog.Extensions.Logging;
using Serilog.Formatting.Json;
using turnero_peluqueria.Data;
using turnero_peluqueria.DTOs;
using turnero_peluqueria.Exceptions;
using turnero_peluqueria.Security;
using turnero_peluqueria.Services;

const string comandoAdministrador = "--crear-administrador";
var crearAdministrador = args.Contains(comandoAdministrador, StringComparer.Ordinal);
var builder = WebApplication.CreateBuilder(args.Where(a => a != comandoAdministrador).ToArray());

builder.Services.AddSingleton<ILoggerProvider>(servicios =>
{
    var configuracion = servicios.GetRequiredService<IConfiguration>();
    var entorno = servicios.GetRequiredService<IWebHostEnvironment>();
    var ruta = Path.Combine(entorno.ContentRootPath, configuracion["ErrorLog:Path"] ?? "logs/errores-.log");
    var logger = new LoggerConfiguration().MinimumLevel.Error()
        .WriteTo.File(new JsonFormatter(renderMessage: true), ruta,
            rollingInterval: RollingInterval.Day, fileSizeLimitBytes: 10 * 1024 * 1024,
            rollOnFileSizeLimit: true, retainedFileCountLimit: 7)
        .CreateLogger();
    return new SerilogLoggerProvider(logger, dispose: true);
});

builder.Services.AddDbContext<AppDbContext>(opciones =>
{
    var conexion = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection para acceder a la base de datos.");
    opciones.UseSqlServer(conexion);
});

builder.Services.AgregarAutenticacion(builder.Configuration);
builder.Services.AddScoped<BajasService>();
builder.Services.AddScoped<TurnosService>();
builder.Services.AddScoped<VetosService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "API de turnero de peluqueria canina",
        Version = "v1"
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    options.AddSecurityRequirement(documento => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", documento)] = []
    });
    options.OperationFilter<AutenticacionSwaggerFiltro>();
});

var app = builder.Build();

if (crearAdministrador)
{
    try
    {
        await using var alcance = app.Services.CreateAsyncScope();
        var servicio = alcance.ServiceProvider.GetRequiredService<AutenticacionService>();
        var datos = app.Configuration.GetSection("Bootstrap");
        await servicio.CrearAdministradorInicialAsync(new CrearAdministradorSolicitud
        {
            Username = datos["Username"] ?? string.Empty,
            Nombre = datos["Nombre"] ?? string.Empty,
            Apellido = datos["Apellido"] ?? string.Empty,
            Email = datos["Email"] ?? string.Empty,
            Contrasena = datos["Password"] ?? string.Empty
        }, CancellationToken.None);
        Console.WriteLine("Administrador inicial creado.");
    }
    catch (ApiException ex)
    {
        Console.Error.WriteLine(ex.Message);
        Environment.ExitCode = 1;
    }
    catch (InvalidOperationException ex)
    {
        Console.Error.WriteLine(ex.Message);
        Environment.ExitCode = 1;
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "No se pudo crear el administrador inicial.");
        Console.Error.WriteLine("No se pudo crear el administrador. Revisá la configuración y las migraciones.");
        Environment.ExitCode = 1;
    }
    await app.DisposeAsync();
    return;
}

// Validar la configuración de autenticación antes de aceptar solicitudes.
_ = app.Services.GetRequiredService<JwtSettings>();
app.UseMiddleware<MiddlewareErrores>();
app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
