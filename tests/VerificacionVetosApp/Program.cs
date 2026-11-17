using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Turnero.App.Seguridad;
using Turnero.App.Turnos;
using Turnero.App.Vetos;
using Turnero.App.Tutores;
using Turnero.App.Veterinarios;

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
