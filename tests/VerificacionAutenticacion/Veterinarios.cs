using System.Net;
using System.Net.Http.Json;
using turnero_peluqueria.DTOs;
using turnero_peluqueria.Entities;

namespace turnero_peluqueria.Verificaciones;

internal static partial class VerificacionAutenticacion
{
    private static async Task VerificarVeterinariosAsync(HttpClient cliente, string tokenAdmin, string tokenVet)
    {
        const string ruta = "/api/veterinarios";
        var datos = new CrearVeterinarioSolicitud
        {
            Username = "vet-nuevo", Email = "vet-nuevo@turnero.test", Nombre = "Ana", Apellido = "Prueba",
            Matricula = 745, Contrasena = ContrasenaInicial
        };

        Autorizar(cliente, tokenVet);
        await EsperarCodigo(cliente.PostAsJsonAsync(ruta, datos), HttpStatusCode.Forbidden, "El alta de veterinarios exige rol administrador");
        Autorizar(cliente, tokenAdmin);
        await EsperarCodigo(cliente.PostAsJsonAsync(ruta, datos with { Matricula = 0 }), HttpStatusCode.BadRequest, "Matrícula inválida");
        using var alta = await cliente.PostAsJsonAsync(ruta, datos);
        Verificar(alta.StatusCode == HttpStatusCode.Created && alta.Headers.Location is not null, "Alta con enlace al perfil creado");
        var perfil = (await alta.Content.ReadFromJsonAsync<VeterinarioRespuesta>())!;
        await EsperarCodigo(cliente.PostAsJsonAsync(ruta, datos with { Username = "otro-vet", Email = "otro-vet@turnero.test" }),
            HttpStatusCode.Conflict, "Matrícula repetida");
        await EsperarCodigo(cliente.PostAsJsonAsync(ruta, datos with { Matricula = 746, Email = "otro-vet@turnero.test" }),
            HttpStatusCode.Conflict, "Nombre de usuario repetido");
        await EsperarCodigo(cliente.PutAsJsonAsync($"{ruta}/{perfil.Id}", new ModificarVeterinarioSolicitud
        {
            Username = datos.Username, Email = datos.Email, Nombre = "Andrea", Apellido = datos.Apellido, Matricula = 746
        }), HttpStatusCode.NoContent, "Edición de veterinario");
        var editado = await cliente.GetFromJsonAsync<VeterinarioRespuesta>(alta.Headers.Location);
        Verificar(editado is { Nombre: "Andrea", Matricula: 746 }, "Se deben devolver los datos editados");
        Autorizar(cliente, null);
        var sesion = await IniciarSesionAsync(cliente, datos.Email);
        Verificar(sesion.Usuario.Rol == Usuario.RolVeterinario && sesion.Usuario.VeterinarioId == perfil.Id,
            "El alta debe crear una cuenta utilizable con su perfil");
    }
}
