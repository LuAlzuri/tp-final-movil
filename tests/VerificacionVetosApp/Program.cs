using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Turnero.App.Seguridad;
using Turnero.App.Turnos;
using Turnero.App.Vetos;
using Turnero.App.Tutores;
using Turnero.App.Veterinarios;
using Turnero.App.Auditoria;

var comprobaciones = 0;
void Verificar(bool condicion, string detalle)
{
    if (!condicion) throw new InvalidOperationException(detalle);
    comprobaciones++;
}
var transporte = new Transporte();
using var cliente = new HttpClient(transporte) { BaseAddress = new Uri("https://pruebas.local/") };
using var sesion = new SesionService(cliente, new Almacen());
await sesion.IniciarSesionAsync("vet@pruebas.local", "contraseña-prueba");
var gestion = new ApiGestion(sesion);
var vetos = new VetosApi(gestion);
var turnos = new TurnosApi(gestion);
transporte.Responder = async solicitud =>
{
    Verificar(solicitud.Headers.Authorization?.ToString() == "Bearer sesion-prueba", "Los clientes reutilizan la sesión autenticada");
    var ruta = solicitud.RequestUri!.PathAndQuery;
    if (solicitud.Method == HttpMethod.Get)
    {
        if (ruta.EndsWith("vigente")) return Transporte.Json(HttpStatusCode.OK, new { veto = (object?)null });
        Verificar(ruta == "/api/tutores/7/vetos?pagina=3&tamano=20", "El historial conserva la paginación solicitada");
        return Transporte.Json(HttpStatusCode.OK, Array.Empty<Veto>());
    }
    var cuerpo = await solicitud.Content!.ReadFromJsonAsync<JsonElement>();
    if (ruta.Contains("turnos"))
    {
        Verificar(ruta == "/api/turnos/presencial", "La reserva presencial usa su endpoint exclusivo");
        Verificar(cuerpo.GetProperty("perroId").GetInt32() == 9 && cuerpo.GetProperty("cupoId").GetInt32() == 12
            && cuerpo.GetProperty("servicioIds").GetArrayLength() == 2, "La reserva conserva mascota, cupo y servicios");
    }
    else
    {
        Verificar(ruta is "/api/tutores/7/vetos" or "/api/tutores/7/vetos/4/levantar", "Las operaciones usan el tutor y veto seleccionados");
        Verificar(cuerpo.GetProperty("motivo").ValueKind == JsonValueKind.Null, "El motivo es opcional en ambas operaciones");
    }
    return new(HttpStatusCode.NoContent);
};
Verificar(await vetos.VigenteAsync(7, default) is null, "La ausencia de veto tiene respuesta explícita");
await vetos.ListarAsync(7, 3, default);
await vetos.GuardarAsync(7, null, null, default);
await vetos.GuardarAsync(7, 4, null, default);
await turnos.ReservarPresencialAsync(9, 12, [1, 2], default);

var consultasVigentes = new System.Collections.Concurrent.ConcurrentDictionary<int, byte>();
var tutoresPrueba = Enumerable.Range(1, 101)
    .Select(id => new Tutor(id, $"tutor{id}", $"tutor{id}@pruebas.local", "Tutor", $"Apellido {id}", [])).ToList();
transporte.Responder = solicitud =>
{
    var ruta = solicitud.RequestUri!.AbsolutePath;
    if (ruta == "/api/tutores")
    {
        var segunda = solicitud.RequestUri.Query.Contains("pagina=2&");
        return Task.FromResult(Transporte.Json(HttpStatusCode.OK, segunda ? tutoresPrueba.Skip(100).ToList() : tutoresPrueba.Take(100).ToList()));
    }
    if (!ruta.EndsWith("/vetos/vigente")) throw new InvalidOperationException("Se consultó el historial en lugar del veto vigente");
    var id = int.Parse(ruta.Split('/')[3]);
    consultasVigentes.TryAdd(id, 0);
    var veto = id == 101 ? new Veto(1, id, null, DateTime.UtcNow, "Automático", null, null) : null;
    return Task.FromResult(Transporte.Json(HttpStatusCode.OK, new EstadoVeto(veto)));
};
var todosLosTutores = await new TutoresApi(gestion).ListarAsync(default);
var tutoresVetados = await vetos.FiltrarTutoresVetadosAsync(todosLosTutores, default);
Verificar(todosLosTutores.Count == 101, "La selección considera todas las páginas de tutores");
Verificar(tutoresVetados.Select(t => t.Id).SequenceEqual([101]), "Solo se permite elegir tutores con veto vigente, incluso fuera de la primera página");
Verificar(consultasVigentes.Count == 101, "El filtro consulta el estado vigente de cada tutor y no una página del historial");
transporte.Responder = _ => Task.FromResult(Transporte.Json(HttpStatusCode.OK, new EstadoVeto(null)));
Verificar((await vetos.FiltrarTutoresVetadosAsync(tutoresPrueba, default)).Count == 0, "Una consulta exitosa sin vetos devuelve la lista vacía");
transporte.Responder = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
await ErrorAsync(() => vetos.FiltrarTutoresVetadosAsync(tutoresPrueba, default), HttpStatusCode.InternalServerError, "no pudo completar");
transporte.Responder = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
await ErrorAsync(() => vetos.FiltrarTutoresVetadosAsync(tutoresPrueba, default), HttpStatusCode.Forbidden, "permiso para gestionar vetos");

async Task ErrorAsync(Func<Task> accion, HttpStatusCode? estado, string texto)
{
    try { await accion(); throw new InvalidOperationException("Faltó rechazar la operación"); }
    catch (GestionApiException ex) { Verificar(ex.Estado == estado && ex.Message.Contains(texto), "Se conserva el error y su estado HTTP"); }
}
await ErrorAsync(() => vetos.GuardarAsync(7, null, new string('a', 501), default), null, "500");
transporte.Responder = _ => Task.FromResult(Transporte.Json(HttpStatusCode.BadRequest,
    new { errors = new Dictionary<string, string[]> { ["Motivo"] = ["El motivo admite hasta 500 caracteres."] } }));
await ErrorAsync(() => vetos.GuardarAsync(7, null, "motivo", default), HttpStatusCode.BadRequest, "500 caracteres");
foreach (var estado in new[] { HttpStatusCode.BadRequest, HttpStatusCode.Conflict })
{
    transporte.Responder = _ => Task.FromResult(Transporte.Json(estado, new { error = "Revisá el estado actualizado." }));
    await ErrorAsync(() => vetos.GuardarAsync(7, 4, null, default), estado, "estado actualizado");
}
transporte.Responder = _ => Task.FromResult(Transporte.Json(HttpStatusCode.Forbidden, new { error = "Comunicate con la veterinaria." }));
await ErrorAsync(() => turnos.SolicitarAsync(9, 12, [1], default), HttpStatusCode.Forbidden, "veterinaria");
await ErrorAsync(() => vetos.VigenteAsync(7, default), HttpStatusCode.Forbidden, "permiso para gestionar vetos");

// Se validan los modelos reales usados por EditForm, sin duplicar sus reglas.
foreach (var modelo in new object[] { new DatosAltaTutor(), new DatosAltaVeterinario() })
{
    var propiedad = modelo.GetType().GetProperty("Contrasena")!;
    foreach (var longitud in new[] { 11, 12, 128, 129 })
    {
        var errores = new List<ValidationResult>();
        var valido = Validator.TryValidateProperty(new string('a', longitud),
            new ValidationContext(modelo) { MemberName = propiedad.Name }, errores);
        Verificar(valido == (longitud is >= 12 and <= 128), "El cambio informativo conserva los límites del cliente");
        if (longitud == 129) Verificar(errores.Any(e => e.ErrorMessage!.Contains("128")), "El error de longitud máxima sigue disponible");
    }
}
foreach (var estado in Enum.GetValues<EstadoTurno>())
{
    var turno = new Turno(1, 9, "Luna", estado, estado.ToString(), DateOnly.FromDateTime(DateTime.UtcNow), 1, null, null, [], []);
    Verificar(turno.PuedeCancelarTutor == (estado is EstadoTurno.Pendiente or EstadoTurno.Aprobado),
        "El tutor conserva la cancelación de turnos normales pendientes o aprobados");
    Verificar(!(turno with { EsPresencial = true }).PuedeCancelarTutor,
        "El tutor nunca dispone de cancelación para reservas presenciales");
}
var recibido = JsonSerializer.Deserialize<Turno>("""
    {"id":1,"perroId":9,"perroNombre":"Luna","estadoId":2,"estadoDescripcion":"Aprobado",
    "fechaCupo":"2026-11-20","cuposReservados":1,"servicios":[],"tratamientos":[],"esPresencial":true}
    """, new JsonSerializerOptions(JsonSerializerDefaults.Web));
Verificar(recibido is { EsPresencial: true, PuedeCancelarTutor: false }, "El cliente recibe el origen presencial desde la API");
var fechaUtc = DateTimeOffset.Parse("2026-11-17T19:30:45Z");
var errorAuditoria = new ErrorAuditoria(fechaUtc, "Error", "Prueba", null, null, null, null);
var movimiento = new MovimientoAuditoria(1, 1, null, "AplicarVeto", "Veto", "1", fechaUtc, null, null);
Verificar(errorAuditoria.FechaArgentina == "17/11/2026 16:30:45", "Los errores muestran hora argentina y conservan los segundos");
Verificar(movimiento.FechaArgentina == errorAuditoria.FechaArgentina, "Ambas grillas de auditoría muestran la misma zona horaria");
Verificar(errorAuditoria.FechaUtc == fechaUtc && movimiento.FechaUtc == fechaUtc, "La presentación no modifica el instante recibido de la API");
Verificar(HoraAuditoria.Formatear(DateTimeOffset.Parse("2026-11-17T01:15:00Z")) == "16/11/2026 22:15:00",
    "La conversión también ajusta el día al cruzar medianoche");
Verificar(HoraAuditoria.Formatear(DateTimeOffset.Parse("2027-01-01T01:00:00Z")) == "31/12/2026 22:00:00",
    "La conversión ajusta el mes y el año");
Verificar(HoraAuditoria.Formatear(DateTimeOffset.Parse("2026-11-17T20:30:45+01:00")) == errorAuditoria.FechaArgentina,
    "La hora del servidor no altera la presentación del mismo instante");
var opcionesJson = new JsonSerializerOptions(JsonSerializerDefaults.Web);
var errorRecibido = JsonSerializer.Deserialize<ErrorAuditoria>("""
    {"fechaUtc":"2026-11-17T19:30:45Z","nivel":"Error","mensaje":"Prueba"}
    """, opcionesJson);
var movimientoRecibido = JsonSerializer.Deserialize<MovimientoAuditoria>("""
    {"id":1,"accion":"AplicarVeto","entidad":"Veto","fechaUtc":"2026-11-17T19:30:45Z"}
    """, opcionesJson);
Verificar(errorRecibido?.FechaArgentina == "17/11/2026 16:30:45" && movimientoRecibido?.FechaArgentina == errorRecibido.FechaArgentina,
    "Las respuestas JSON de errores y movimientos mantienen el contrato UTC y se presentan en Argentina");
Console.WriteLine($"Clientes y formularios verificados: {comprobaciones} comprobaciones.");

public interface ISecureStorage
{
    Task<string?> GetAsync(string clave);
    Task SetAsync(string clave, string valor);
    bool Remove(string clave);
}
sealed class Almacen : ISecureStorage
{
    private string? valor;
    public Task<string?> GetAsync(string clave) => Task.FromResult(valor);
    public Task SetAsync(string clave, string nuevo) { valor = nuevo; return Task.CompletedTask; }
    public bool Remove(string clave) { valor = null; return true; }
}
sealed class Transporte : HttpMessageHandler
{
    public Func<HttpRequestMessage, Task<HttpResponseMessage>>? Responder { get; set; }
    public static HttpResponseMessage Json(HttpStatusCode estado, object datos) => new(estado) { Content = JsonContent.Create(datos) };
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage solicitud, CancellationToken ct) =>
        solicitud.RequestUri!.AbsolutePath.EndsWith("iniciar-sesion")
            ? Task.FromResult(Json(HttpStatusCode.OK, new { token = "sesion-prueba", expiraUtc = DateTimeOffset.UtcNow.AddMinutes(5),
                usuario = new { id = 1, username = "vet", email = "vet@pruebas.local", rol = Roles.Veterinario } }))
            : Responder!(solicitud);
}
