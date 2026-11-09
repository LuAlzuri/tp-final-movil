using System.Net;
using System.Net.Http.Json;
using turnero_peluqueria.DTOs;
using turnero_peluqueria.Entities;

namespace turnero_peluqueria.Verificaciones;

internal static partial class VerificacionAutenticacion
{
    private static async Task VerificarTutoresAsync(HttpClient cliente, string tokenAdmin, string tokenVet)
    {
        const string ruta = "/api/tutores";
        var telefono = new TelefonoTutorSolicitud { NombreProp = "Ana", NroTel = "1111111111" };
        var datos = new CrearTutorSolicitud
        {
            Username = "tutor-nuevo", Email = "tutor-nuevo@turnero.test", Nombre = "Ana", Apellido = "Prueba",
            Contrasena = ContrasenaInicial, Telefonos = [telefono]
        };

        Autorizar(cliente, tokenAdmin);
        await EsperarCodigo(cliente.PostAsJsonAsync(ruta, datos), HttpStatusCode.Forbidden, "Solo un veterinario puede dar de alta un tutor");
        Autorizar(cliente, tokenVet);
        await EsperarCodigo(cliente.PostAsJsonAsync(ruta, datos with { Telefonos = [] }), HttpStatusCode.BadRequest, "El alta requiere teléfono");
        await EsperarCodigo(cliente.PostAsJsonAsync(ruta, datos with { Telefonos = [null] }), HttpStatusCode.BadRequest, "No se admite un teléfono nulo");
        await EsperarCodigo(cliente.PostAsJsonAsync(ruta, datos with { Telefonos = [telefono, telefono] }), HttpStatusCode.Conflict, "El alta rechaza teléfonos repetidos");
        using var alta = await cliente.PostAsJsonAsync(ruta, datos);
        Verificar(alta.StatusCode == HttpStatusCode.Created && alta.Headers.Location is not null, "Alta de tutor con teléfono");
        var perfil = (await alta.Content.ReadFromJsonAsync<TutorRespuesta>())!;
        var telefonos = $"{ruta}/{perfil.Id}/telefonos";
        await EsperarCodigo(cliente.PostAsJsonAsync(telefonos, telefono), HttpStatusCode.Conflict, "Teléfono repetido en el mismo tutor");
        using var altaTelefono = await cliente.PostAsJsonAsync(telefonos, telefono with { NroTel = "2222222222" });
        Verificar(altaTelefono.StatusCode == HttpStatusCode.Created && altaTelefono.Headers.Location is not null, "Alta de un segundo teléfono");
        await EsperarCodigo(cliente.PutAsJsonAsync(altaTelefono.Headers.Location, telefono with { NroTel = "3333333333" }),
            HttpStatusCode.NoContent, "Edición de teléfono");
        var guardado = await cliente.GetFromJsonAsync<TutorRespuesta>(alta.Headers.Location);
        Verificar(guardado?.Telefonos.Count == 2 && guardado.Telefonos.Any(t => t.NroTel == "3333333333"),
            "La consulta debe devolver los teléfonos actualizados");
        var listas = await cliente.GetFromJsonAsync<List<TutorRespuesta>>(ruta);
        Verificar(listas?.Any(t => t.Id == perfil.Id) == true, "El tutor debe aparecer en la lista");
        Autorizar(cliente, null);
        var sesion = await IniciarSesionAsync(cliente, datos.Email);
        Verificar(sesion.Usuario.Rol == Usuario.RolTutor && sesion.Usuario.TutorId == perfil.Id, "La cuenta nueva debe poder iniciar sesión");
        Autorizar(cliente, sesion.Token);
        await EsperarCodigo(cliente.GetAsync(ruta), HttpStatusCode.Forbidden, "Un tutor no puede administrar tutores");
    }
}
