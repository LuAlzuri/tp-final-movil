using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using turnero_peluqueria.Data;
using turnero_peluqueria.DTOs;
using turnero_peluqueria.Entities;

namespace turnero_peluqueria.Verificaciones;

internal static partial class VerificacionAutenticacion
{
    private static async Task VerificarCatalogosDeTurnosAsync(FabricaApi fabrica, HttpClient cliente, string tokenAdmin)
    {
        await using var alcance = fabrica.Services.CreateAsyncScope();
        var db = alcance.ServiceProvider.GetRequiredService<AppDbContext>();
        var turno = await db.Turnos.AsNoTracking().SingleAsync();
        db.Tutores.Add(new Tutor { Usuario = NuevaCuenta("tutor-servicios", Usuario.RolTutor) });
        await db.SaveChangesAsync();

        Autorizar(cliente, null);
        var vet = await IniciarSesionAsync(cliente, "vet-nuevo@turnero.test");
        var tutor = await IniciarSesionAsync(cliente, "tutor-servicios@turnero.test");
        var datos = new CatalogoSolicitud { Descripcion = "Entrada para turno" };

        foreach (var (ruta, entidad, vincular) in new (string Ruta, string Entidad, Func<int, object> Vincular)[]
        {
            ("/api/patologias", nameof(Patologia), id => new PerroPatologia { PerroId = turno.PerroId, PatologiaId = id }),
            ("/api/tratamientos", nameof(Tratamiento), id => new TratamientoPorTurno { TurnoId = turno.Id, TratamientoId = id }),
            ("/api/servicios", nameof(Servicio), id => new ServicioPorTurno { TurnoId = turno.Id, ServicioId = id })
        })
        {
            Autorizar(cliente, vet.Token);
            using var alta = await cliente.PostAsJsonAsync(ruta, datos);
            Verificar(alta.StatusCode == HttpStatusCode.Created && alta.Headers.Location is not null,
                "El veterinario puede dar de alta una entrada");
            var entrada = (await alta.Content.ReadFromJsonAsync<CatalogoRespuesta>())!;
            await EsperarCodigo(cliente.PostAsJsonAsync(ruta, datos), HttpStatusCode.Conflict, "Descripción repetida");

            Autorizar(cliente, tutor.Token);
            var permisoLectura = entidad == nameof(Servicio) ? HttpStatusCode.OK : HttpStatusCode.Forbidden;
            await EsperarCodigo(cliente.GetAsync(ruta), permisoLectura, "El tutor puede consultar los servicios");
            await EsperarCodigo(cliente.PutAsJsonAsync(alta.Headers.Location, datos), HttpStatusCode.Forbidden,
                "El tutor no puede editar catálogos");

            Autorizar(cliente, tokenAdmin);
            await EsperarCodigo(cliente.PutAsJsonAsync(alta.Headers.Location, datos with { Descripcion = "Entrada actualizada" }),
                HttpStatusCode.NoContent, "El administrador puede editar la entrada");
            var vinculo = vincular(entrada.Id);
            db.Add(vinculo);
            await db.SaveChangesAsync();
            await EsperarCodigo(cliente.DeleteAsync(alta.Headers.Location), HttpStatusCode.Conflict, "Un vínculo histórico bloquea el borrado");
            db.Remove(vinculo);
            await db.SaveChangesAsync();
            await EsperarCodigo(cliente.DeleteAsync(alta.Headers.Location), HttpStatusCode.NoContent, "Se puede eliminar una entrada libre");

            var auditoria = await cliente.GetFromJsonAsync<List<MovimientoAuditoriaRespuesta>>(
                $"/api/auditoria/movimientos?entidad={entidad}");
            Verificar(auditoria?.Select(a => a.Accion).SequenceEqual(new[] { "Eliminar", "Modificar", "Crear" }) == true,
                "La auditoría debe incluir solo los cambios efectivos");
        }
    }
}
