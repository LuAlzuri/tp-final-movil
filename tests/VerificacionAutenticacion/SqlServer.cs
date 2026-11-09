using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using turnero_peluqueria.Data;
using turnero_peluqueria.DTOs;
using turnero_peluqueria.Entities;
using turnero_peluqueria.Services;

namespace turnero_peluqueria.Verificaciones;

internal static partial class VerificacionAutenticacion
{
    private static async Task VerificarSqlServerAsync()
    {
        var servidor = Environment.GetEnvironmentVariable("TURNERO_SQLSERVER_PRUEBAS");
        if (string.IsNullOrWhiteSpace(servidor))
            throw new InvalidOperationException("Configurá TURNERO_SQLSERVER_PRUEBAS para una instancia de pruebas.");

        var conexion = new SqlConnectionStringBuilder(servidor);
        if (conexion.AttachDBFilename.Length != 0)
            throw new InvalidOperationException("La conexión de pruebas debe apuntar a una instancia, sin adjuntar archivos.");
        conexion.InitialCatalog = "turnero_pruebas_" + Guid.NewGuid().ToString("N");
        var opciones = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(conexion.ConnectionString).Options;
        await using var db = new AppDbContext(opciones);
        try
        {
            await db.Database.MigrateAsync();
            Verificar(!(await db.Database.GetPendingMigrationsAsync()).Any(), "La cadena completa debe aplicarse a SQL Server");
            await VerificarConcurrenciaAsync(db, conexion.ConnectionString);
            Console.WriteLine($"SQL Server y Kestrel verificados: {verificaciones} comprobaciones.");
        }
        finally
        {
            // Solo se elimina la base con el nombre aleatorio reservado para esta ejecución.
            await db.Database.EnsureDeletedAsync();
        }
    }

    private static async Task VerificarConcurrenciaAsync(AppDbContext db, string conexion)
    {
        var sincronizador = new EncuentroSql();
        await using var fabrica = new FabricaApi(conexionSql: conexion, interceptor: sincronizador);
        fabrica.UseKestrel(opciones => opciones.Listen(IPAddress.Loopback, 0));
        using var primero = fabrica.CreateClient();
        using var segundo = fabrica.CreateClient();
        using var veterinario = fabrica.CreateClient();

        var vet = new Veterinario { Usuario = NuevaCuenta("vet-sql", Usuario.RolVeterinario), Matricula = 574 };
        var hoy = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow,
            "America/Argentina/Buenos_Aires"));
        var cupo = new CupoDiario { FechaCupo = hoy.AddDays(1), NroCupoDis = 1 };
        var servicio = new Servicio { Descripcion = "Baño de prueba" };
        Perro CrearPerro(string cuenta, string nombre) => new()
        {
            Tutor = new Tutor { Usuario = NuevaCuenta(cuenta, Usuario.RolTutor) },
            Nombre = nombre, FechaNacimiento = hoy.AddYears(-2), GrupoEtarioId = 2, TamanioId = 2
        };
        var perroUno = CrearPerro("tutor-sql-uno", "Luna");
        var perroDos = CrearPerro("tutor-sql-dos", "Tita");
        db.AddRange(vet, cupo, servicio, perroUno, perroDos);
        await db.SaveChangesAsync();

        Autorizar(primero, (await IniciarSesionAsync(primero, perroUno.Tutor.Usuario.Email)).Token);
        Autorizar(segundo, (await IniciarSesionAsync(segundo, perroDos.Tutor.Usuario.Email)).Token);
        Autorizar(veterinario, (await IniciarSesionAsync(veterinario, vet.Usuario.Email)).Token);
        SolicitarTurnoSolicitud Solicitud(Perro perro) =>
            new() { PerroId = perro.Id, CupoId = cupo.Id, ServicioIds = [servicio.Id] };

        sincronizador.Activar(sql => sql.Contains("FROM [CuposDiarios]", StringComparison.Ordinal));
        var reservas = await Task.WhenAll(primero.PostAsJsonAsync("/api/turnos", Solicitud(perroUno)),
            segundo.PostAsJsonAsync("/api/turnos", Solicitud(perroDos)));
        using var reservaUno = reservas[0];
        using var reservaDos = reservas[1];
        Verificar(reservas.Count(r => r.StatusCode == HttpStatusCode.Created) == 1
            && reservas.Count(r => r.StatusCode == HttpStatusCode.Conflict) == 1, "Solo una solicitud puede reservar el último cupo");
        Verificar(await db.CuposTurnos.CountAsync(r => r.CupoId == cupo.Id) == 1
            && await db.CuposTurnos.Where(r => r.CupoId == cupo.Id).SumAsync(r => r.Cantidad) == 1
            && await db.ServiciosPorTurno.CountAsync() == 1,
            "La solicitud rechazada no deja una reserva ni servicios parciales");

        var rutaImagen = $"/api/perros/{perroUno.Id}/imagen";
        byte[] Png(SKColor color)
        {
            using var superficie = SKSurface.Create(new SKImageInfo(16, 16));
            superficie.Canvas.Clear(color);
            using var imagen = superficie.Snapshot();
            using var datos = imagen.Encode(SKEncodedImageFormat.Png, 100);
            return datos.ToArray();
        }

        var azul = Png(SKColors.Blue);
        var verde = Png(SKColors.Green);
        async Task<HttpResponseMessage> SubirAsync(byte[] datos, bool excederCuerpo = false)
        {
            using var formulario = new MultipartFormDataContent();
            var imagen = new ByteArrayContent(datos);
            imagen.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            formulario.Add(imagen, "archivo", "prueba.png");
            if (excederCuerpo)
                formulario.Add(new ByteArrayContent(new byte[ImagenPerroService.MaxPeticion]), "relleno", "relleno.bin");
            using var peticion = new HttpRequestMessage(HttpMethod.Post, rutaImagen) { Content = formulario };
            peticion.Headers.ExpectContinue = excederCuerpo;
            return await veterinario.SendAsync(peticion);
        }

        await EsperarCodigo(SubirAsync(azul, excederCuerpo: true), HttpStatusCode.RequestEntityTooLarge,
            "Kestrel debe rechazar el cuerpo completo aunque la imagen sea pequeña");
        Verificar(!await db.Imagenes.AnyAsync(), "Un cuerpo rechazado no persiste imágenes");

        var nombresDePrueba = new List<string>();
        try
        {
            using var inicial = await SubirAsync(azul);
            Verificar(inicial.StatusCode == HttpStatusCode.OK, "El perro debe tener una imagen para probar el reemplazo");
            var anterior = (await inicial.Content.ReadFromJsonAsync<ImagenResponse>())!;
            nombresDePrueba.Add(anterior.NombreArchivo);

            sincronizador.Activar(sql => sql.TrimStart().StartsWith("UPDATE", StringComparison.Ordinal)
                && sql.Contains("[Perros]", StringComparison.Ordinal));
            var subidas = await Task.WhenAll(SubirAsync(azul), SubirAsync(verde));
            using var subidaUno = subidas[0];
            using var subidaDos = subidas[1];
            foreach (var subida in subidas.Where(s => s.IsSuccessStatusCode))
                nombresDePrueba.Add((await subida.Content.ReadFromJsonAsync<ImagenResponse>())!.NombreArchivo);

            Verificar(subidas.All(s => s.StatusCode == HttpStatusCode.OK), "Las dos subidas deben poder reemplazar en serie");
            var vigente = await db.Imagenes.AsNoTracking().SingleAsync(i => i.PerroId == perroUno.Id);
            Verificar(vigente.Id == anterior.Id && nombresDePrueba.Skip(1).Contains(vigente.NombreArchivo),
                "El reemplazo concurrente conserva un único registro");
            var esperado = vigente.NombreArchivo == nombresDePrueba[1] ? azul : verde;
            var contenido = await veterinario.GetByteArrayAsync(vigente.RutaRelativa);
            Verificar(contenido.SequenceEqual(esperado), "La imagen vigente conserva los bytes de la subida correspondiente");
            foreach (var obsoleta in nombresDePrueba.Where(n => n != vigente.NombreArchivo))
                await EsperarCodigo(veterinario.GetAsync("/uploads/" + obsoleta), HttpStatusCode.NotFound,
                    "Los archivos reemplazados deben limpiarse");
        }
        finally
        {
            var almacen = fabrica.Services.GetRequiredService<IAlmacenImagenes>();
            foreach (var nombre in nombresDePrueba)
                almacen.IntentarEliminar(nombre);
        }
    }

    private sealed class EncuentroSql : DbCommandInterceptor
    {
        private Encuentro? encuentro;

        public void Activar(Func<string, bool> coincide) => encuentro = new Encuentro(coincide);

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            await EsperarAsync(command, cancellationToken);
            return result;
        }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            await EsperarAsync(command, cancellationToken);
            return result;
        }

        private async Task EsperarAsync(DbCommand comando, CancellationToken ct)
        {
            var actual = encuentro;
            if (actual is null || !actual.Coincide(comando.CommandText))
                return;

            if (Interlocked.Increment(ref actual.Llegadas) == 2)
                actual.Listo.TrySetResult();
            await actual.Listo.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
        }

        private sealed class Encuentro(Func<string, bool> coincide)
        {
            public Func<string, bool> Coincide { get; } = coincide;
            public TaskCompletionSource Listo { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public int Llegadas;
        }
    }
}
