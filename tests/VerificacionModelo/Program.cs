using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using turnero_peluqueria.Data;
using turnero_peluqueria.Entities;

// Verificaciones sin servidor SQL: construyen el modelo y generan SQL, sin ejecutarlo.
using var db = new AppDbContextFactory().CreateDbContext([]);
var modelo = db.GetService<IDesignTimeModel>().Model;
var verificaciones = 0;

void Verificar(bool condicion, string mensaje)
{
    if (!condicion) throw new InvalidOperationException(mensaje);
    verificaciones++;
}

IEntityType Entidad<T>() => modelo.FindEntityType(typeof(T))
    ?? throw new InvalidOperationException($"No se encontro la entidad {typeof(T).Name}.");

bool IndiceUnico<T>(params string[] propiedades) => Entidad<T>().GetIndexes().Any(indice =>
    indice.IsUnique && indice.Properties.Select(p => p.Name).SequenceEqual(propiedades));

Verificar(modelo.GetEntityTypes().Count() == 19, "Deben existir 19 entidades, sin tablas implicitas.");
Verificar(!modelo.GetEntityTypes().SelectMany(e => e.GetProperties()).Any(p => p.IsShadowProperty()), "No debe haber columnas implicitas.");
Verificar(modelo.GetEntityTypes().SelectMany(e => e.GetForeignKeys()).All(fk => fk.DeleteBehavior == DeleteBehavior.NoAction), "Ninguna relacion debe borrar en cascada.");
Verificar(modelo.GetEntityTypes().All(e => !e.GetDeclaredQueryFilters().Any()), "Los historiales no deben ocultarse por filtros globales.");

Verificar(IndiceUnico<Usuario>(nameof(Usuario.Email)), "El email debe ser unico.");
Verificar(IndiceUnico<Usuario>(nameof(Usuario.Username)), "El usuario debe ser unico.");
Verificar(IndiceUnico<Veterinario>(nameof(Veterinario.UserId)), "Un usuario solo puede tener un perfil veterinario.");
Verificar(IndiceUnico<Tutor>(nameof(Tutor.UserId)), "Un usuario solo puede tener un perfil tutor.");
Verificar(IndiceUnico<Veterinario>(nameof(Veterinario.Matricula)), "La matricula debe ser unica.");
Verificar(IndiceUnico<TelefonoTutor>(nameof(TelefonoTutor.TutorId), nameof(TelefonoTutor.NroTel)), "El telefono no puede repetirse para el mismo tutor.");
Verificar(IndiceUnico<CupoDiario>(nameof(CupoDiario.FechaCupo)), "Solo puede existir un cupo diario por fecha.");
Verificar(IndiceUnico<CupoTurno>(nameof(CupoTurno.TurnoId)), "Un turno no puede ocupar varios dias.");

var indiceVeto = Entidad<Veto>().GetIndexes().Single(i => i.IsUnique);
Verificar(indiceVeto.GetFilter() == "[fecha_fin] IS NULL" && indiceVeto.Properties.Single().Name == nameof(Veto.TutorId), "Debe existir un solo veto vigente por tutor.");
foreach (var tipo in new[] { typeof(PerroPatologia), typeof(CupoTurno), typeof(TratamientoPorTurno), typeof(ServicioPorTurno) })
    Verificar(modelo.FindEntityType(tipo)!.FindPrimaryKey()!.Properties.Count == 2, $"La clave de {tipo.Name} debe ser compuesta.");

Verificar(Entidad<Perro>().FindProperty(nameof(Perro.FechaNacimiento))!.GetColumnType() == "date", "El nacimiento es una fecha civil.");
Verificar(Entidad<CupoDiario>().FindProperty(nameof(CupoDiario.FechaCupo))!.GetColumnType() == "date", "El cupo corresponde a una fecha civil.");
Verificar(Entidad<Turno>().FindProperty("CupoId") is null && Entidad<Turno>().FindProperty("TratamientoId") is null, "Los cupos y tratamientos deben utilizar tablas intermedias.");
Verificar(Entidad<Turno>().FindProperty(nameof(Turno.VeterinarioId))!.IsNullable, "El turno pendiente no requiere veterinario.");
Verificar(Entidad<Veto>().FindProperty(nameof(Veto.VeterinarioId))!.IsNullable, "El veto automatico no tiene veterinario.");
Verificar(Entidad<AuditoriaAccion>().FindProperty(nameof(AuditoriaAccion.UsuarioId))!.IsNullable, "La auditoria debe admitir acciones automaticas.");

var propiedadesUtc = modelo.GetEntityTypes().SelectMany(e => e.GetProperties())
    .Where(p => (Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType) == typeof(DateTime)).ToArray();
Verificar(propiedadesUtc.Length == 5, "Se esperan cinco marcas temporales UTC.");
foreach (var propiedad in propiedadesUtc)
{
    var conversor = propiedad.GetValueConverter()!;
    Verificar(conversor is not null, $"{propiedad.Name} debe preservar UTC.");
    var valorUtc = new DateTime(2026, 11, 8, 15, 0, 0, DateTimeKind.Utc);
    Verificar((DateTime)conversor!.ConvertToProvider(valorUtc)! == valorUtc, "La escritura no debe desplazar la hora UTC.");
    Verificar(((DateTime)conversor.ConvertFromProvider(DateTime.SpecifyKind(valorUtc, DateTimeKind.Unspecified))!).Kind == DateTimeKind.Utc, "La lectura debe recuperar DateTimeKind.Utc.");
    Verificar(conversor.ConvertToProvider(null) is null, "Las fechas opcionales deben conservar null.");
    var rechazoLocal = false;
    try { conversor.ConvertToProvider(DateTime.SpecifyKind(valorUtc, DateTimeKind.Local)); }
    catch (ArgumentException) { rechazoLocal = true; }
    Verificar(rechazoLocal, "No deben persistirse horas locales como UTC.");
}

var jsonUsuario = JsonSerializer.Serialize(new Usuario { PasswordHash = "hash-privado" });
Verificar(!jsonUsuario.Contains("PasswordHash") && !jsonUsuario.Contains("hash-privado"), "El hash no debe serializarse.");
Verificar(Entidad<Turno>().FindProperty(nameof(Turno.EstadoId))!.GetDefaultValue() is Estado.Pendiente, "El estado inicial debe ser Pendiente.");
Verificar(Entidad<CupoTurno>().FindProperty(nameof(CupoTurno.Cantidad))!.GetDefaultValue() is 1, "La reserva inicial debe ocupar un cupo.");
Verificar(Entidad<Veterinario>().GetCheckConstraints().Any(c => c.Sql == "[Matricula] BETWEEN 1 AND 99999"), "La matricula debe tener entre uno y cinco digitos.");
Verificar(Entidad<CupoDiario>().GetCheckConstraints().Any(c => c.Sql == "[nro_cupo_dis] >= 0"), "El total de cupos no puede ser negativo.");
Verificar(Entidad<CupoTurno>().GetCheckConstraints().Any(c => c.Sql == "[cantidad] >= 1"), "La cantidad reservada debe ser positiva.");
Verificar(Entidad<Veto>().GetCheckConstraints().Any(c => c.Sql == "[fecha_fin] IS NULL OR [fecha_fin] >= [fecha_inicio]"), "El veto no puede terminar antes de comenzar.");
Verificar(Entidad<Veto>().GetCheckConstraints().Any(c => c.Sql == "[fecha_fin] IS NOT NULL OR [motivo_fin_veto] IS NULL"), "No puede haber motivo de retiro sin retiro del veto.");
Verificar(Entidad<Estado>().GetCheckConstraints().Any(c => c.Sql == "[id_estado] BETWEEN 1 AND 6"), "Solo se admiten los seis estados acordados.");
Verificar(Entidad<Turno>().GetCheckConstraints().Any(c => c.Sql == "[estado_id] NOT IN (2, 5, 6) OR [veterinario_id] IS NOT NULL"), "Un turno aprobado o atendido requiere veterinario.");

var ensamblado = db.GetService<IMigrationsAssembly>();
Verificar(ensamblado.Migrations.Count == 20, "Se espera una migracion por entidad y otra para semillas.");
var tablasCreadas = new HashSet<string>();
var filasSemilla = 0;
foreach (var migracionInfo in ensamblado.Migrations.OrderBy(x => x.Key))
{
    var migracion = ensamblado.CreateMigration(migracionInfo.Value, db.Database.ProviderName!);
    var creaciones = migracion.UpOperations.OfType<CreateTableOperation>().ToArray();
    if (migracionInfo.Key.EndsWith("SembrarCatalogos"))
        Verificar(creaciones.Length == 0, "Las semillas deben estar separadas de la creacion de entidades.");
    else
        Verificar(creaciones.Length == 1, $"{migracionInfo.Key} debe crear una sola tabla.");

    foreach (var tabla in creaciones)
    {
        foreach (var fk in tabla.ForeignKeys)
            Verificar(tablasCreadas.Contains(fk.PrincipalTable), $"{tabla.Name} depende de una tabla aun no creada: {fk.PrincipalTable}.");
        Verificar(tablasCreadas.Add(tabla.Name), $"La tabla {tabla.Name} se crea mas de una vez.");
    }
    filasSemilla += migracion.UpOperations.OfType<InsertDataOperation>().Sum(op => op.Values.GetLength(0));
    Verificar(!migracion.UpOperations.Any(op => op is DropTableOperation or DropColumnOperation or AlterColumnOperation), "La cadena inicial no debe contener cambios destructivos ni reconstrucciones.");
}
Verificar(tablasCreadas.Count == 19 && filasSemilla == 12, "Deben crearse 19 tablas e insertarse 12 filas iniciales.");

var estados = Entidad<Estado>().GetSeedData().OrderBy(x => x[nameof(Estado.Id)]).ToArray();
Verificar(estados.Select(x => (int)x[nameof(Estado.Id)]!).SequenceEqual(Enumerable.Range(1, 6)), "Los estados deben conservar los seis identificadores fijos.");
Verificar(estados.Select(x => (string)x[nameof(Estado.Descripcion)]!).SequenceEqual(new[] { "Pendiente", "Aprobado", "Rechazado", "Cancelado por el tutor", "Cancelado por el veterinario", "Completado" }), "Los estados iniciales deben coincidir con el glosario.");
foreach (var tipo in new[] { typeof(Patologia), typeof(Tratamiento), typeof(Servicio) })
    Verificar(!modelo.FindEntityType(tipo)!.GetSeedData().Any(), "No deben precargarse patologias, tratamientos ni servicios.");

var script = db.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
Verificar(script.Contains("SET IDENTITY_INSERT [Estados] ON"), "La semilla de estados debe preservar sus identificadores con IDENTITY_INSERT.");
Verificar(script.Contains("CREATE UNIQUE INDEX [IX_Vetos_tutor_id] ON [Vetos] ([tutor_id]) WHERE [fecha_fin] IS NULL"), "El SQL debe incluir el indice filtrado del veto vigente.");
Verificar(!script.Contains("ON DELETE CASCADE"), "El SQL no debe contener borrados en cascada.");
var reverso = db.GetService<IMigrator>().GenerateScript(ensamblado.Migrations.Keys.Last(), Migration.InitialDatabase);
Verificar(reverso.IndexOf("DROP TABLE [CuposTurnos]", StringComparison.Ordinal) < reverso.IndexOf("DROP TABLE [Turnos]", StringComparison.Ordinal), "La reversion debe respetar las dependencias.");
Verificar(!db.Database.HasPendingModelChanges(), "No debe haber diferencias entre el modelo final y su snapshot.");

Console.WriteLine($"Verificacion correcta: {verificaciones} comprobaciones; sin conexiones a la base de datos.");
