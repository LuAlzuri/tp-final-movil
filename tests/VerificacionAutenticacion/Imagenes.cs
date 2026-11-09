using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using turnero_peluqueria.Data;
using turnero_peluqueria.DTOs;
using turnero_peluqueria.Entities;
using turnero_peluqueria.Services;

namespace turnero_peluqueria.Verificaciones;

internal static partial class VerificacionAutenticacion
{
    private static async Task VerificarImagenesAsync(FabricaApi fabrica, HttpClient cliente, string tokenAdmin)
    {
        Autorizar(cliente, null);
        var vet = await IniciarSesionAsync(cliente, "vet-nuevo@turnero.test");
        var tutor = await IniciarSesionAsync(cliente, "tutor-otro@turnero.test");
        var ajeno = await IniciarSesionAsync(cliente, "tutor-servicios@turnero.test");
        await using var alcance = fabrica.Services.CreateAsyncScope();
        var db = alcance.ServiceProvider.GetRequiredService<AppDbContext>();
        var perro = new Perro
        {
            TutorId = tutor.Usuario.TutorId!.Value, Nombre = "Oso",
            FechaNacimiento = new DateOnly(2020, 1, 1), GrupoEtarioId = 2, TamanioId = 2
        };
        db.Perros.Add(perro);
        await db.SaveChangesAsync();

        var rutaPerro = $"/api/perros/{perro.Id}";
        var rutaImagen = rutaPerro + "/imagen";
        using var superficie = SKSurface.Create(new SKImageInfo(16, 16));
        superficie.Canvas.Clear(SKColors.Blue);
        using var muestra = superficie.Snapshot();
        using var archivoPng = muestra.Encode(SKEncodedImageFormat.Png, 100);
        using var archivoJpg = muestra.Encode(SKEncodedImageFormat.Jpeg, 90);
        var png = archivoPng.ToArray();
        var jpg = archivoJpg.ToArray();

        async Task<HttpResponseMessage> SubirAsync(byte[] bytes, string nombre, string mime, string campo = "archivo")
        {
            using var formulario = new MultipartFormDataContent();
            var archivo = new ByteArrayContent(bytes);
            archivo.Headers.ContentType = new MediaTypeHeaderValue(mime);
            formulario.Add(archivo, campo, nombre);
            return await cliente.PostAsync(rutaImagen, formulario);
        }

        Autorizar(cliente, tutor.Token);
        await EsperarCodigo(SubirAsync(png, "foto.png", "image/png"), HttpStatusCode.Forbidden, "El tutor no puede subir imágenes");
        Autorizar(cliente, tokenAdmin);
        await EsperarCodigo(SubirAsync(png, "foto.png", "image/png"), HttpStatusCode.Forbidden, "El administrador no gestiona las imágenes");
        Autorizar(cliente, vet.Token);
        await EsperarCodigo(SubirAsync(png, "foto.png", "image/png", "file"), HttpStatusCode.BadRequest, "Se exige el campo archivo");
        await EsperarCodigo(SubirAsync(png, "foto.png", "image/jpeg"), HttpStatusCode.BadRequest, "El MIME debe coincidir con la extensión");
        await EsperarCodigo(SubirAsync(jpg, "foto.png", "image/png"), HttpStatusCode.BadRequest, "Se comprueba el formato real");
        await EsperarCodigo(SubirAsync(new byte[ImagenPerroService.MaxTamano + 1], "foto.png", "image/png"),
            HttpStatusCode.RequestEntityTooLarge, "La imagen no puede superar 5 MiB");
        Verificar(!await db.Imagenes.AnyAsync(i => i.PerroId == perro.Id), "Las subidas rechazadas no crean metadatos");

        using var primeraSubida = await SubirAsync(png, "foto.png", "image/png");
        Verificar(primeraSubida.StatusCode == HttpStatusCode.OK, "El veterinario puede subir una imagen PNG");
        var primera = (await primeraSubida.Content.ReadFromJsonAsync<ImagenResponse>())!;
        Verificar(primera.PerroId == perro.Id && primera.TamanoBytes == png.Length
            && primera.ImagenUrl == "/uploads/" + primera.NombreArchivo, "La subida devuelve metadatos y ruta");
        Autorizar(cliente, null);
        using (var archivo = await cliente.GetAsync(primera.ImagenUrl))
        {
            Verificar(archivo.StatusCode == HttpStatusCode.OK && archivo.Content.Headers.ContentType?.MediaType == "image/png"
                && archivo.Headers.TryGetValues("X-Content-Type-Options", out var valores) && valores.Single() == "nosniff"
                && (await archivo.Content.ReadAsByteArrayAsync()).SequenceEqual(png), "La URL pública sirve los bytes con nosniff");
        }

        Autorizar(cliente, tutor.Token);
        var ficha = await cliente.GetFromJsonAsync<PerroRespuesta>(rutaPerro);
        Verificar(ficha?.ImagenUrl == primera.ImagenUrl, "El tutor ve la URL de su perro");
        Autorizar(cliente, ajeno.Token);
        await EsperarCodigo(cliente.GetAsync(rutaPerro), HttpStatusCode.NotFound, "Un tutor ajeno no puede obtener la URL desde la ficha");

        Autorizar(cliente, vet.Token);
        using var reemplazo = await SubirAsync(jpg, "otra.jpeg", "image/jpeg");
        Verificar(reemplazo.StatusCode == HttpStatusCode.OK, "Se puede reemplazar la imagen");
        var nueva = (await reemplazo.Content.ReadFromJsonAsync<ImagenResponse>())!;
        Verificar(nueva.Id == primera.Id && nueva.NombreArchivo.EndsWith(".jpg", StringComparison.Ordinal)
            && nueva.ImagenUrl != primera.ImagenUrl && await db.Imagenes.CountAsync(i => i.PerroId == perro.Id) == 1,
            "El reemplazo conserva un único registro y usa un nuevo nombre de disco");
        await EsperarCodigo(cliente.GetAsync(primera.ImagenUrl), HttpStatusCode.NotFound, "El reemplazo limpia el archivo anterior");
        await EsperarCodigo(cliente.DeleteAsync(rutaImagen), HttpStatusCode.NoContent, "Se puede borrar la imagen");
        await EsperarCodigo(cliente.GetAsync(nueva.ImagenUrl), HttpStatusCode.NotFound, "El borrado limpia el archivo");
        ficha = await cliente.GetFromJsonAsync<PerroRespuesta>(rutaPerro);
        Verificar(ficha is { ImagenUrl: null } && !await db.Imagenes.AnyAsync(i => i.PerroId == perro.Id), "La ficha vuelve a quedar sin imagen");

        using var ultimaSubida = await SubirAsync(png, "foto.png", "image/png");
        Verificar(ultimaSubida.StatusCode == HttpStatusCode.OK, "El perro puede volver a tener imagen");
        var ultima = (await ultimaSubida.Content.ReadFromJsonAsync<ImagenResponse>())!;
        try
        {
            await EsperarCodigo(cliente.DeleteAsync(rutaPerro), HttpStatusCode.NoContent, "La imagen no impide la baja lógica");
            await EsperarCodigo(cliente.GetAsync(rutaPerro), HttpStatusCode.NotFound, "La baja oculta la ficha y su URL");
            await EsperarCodigo(SubirAsync(png, "foto.png", "image/png"), HttpStatusCode.NotFound, "No se suben imágenes a perros dados de baja");
            Verificar(await db.Imagenes.AnyAsync(i => i.Id == ultima.Id), "La baja conserva los metadatos");
            Autorizar(cliente, null);
            await EsperarCodigo(cliente.GetAsync(ultima.ImagenUrl), HttpStatusCode.OK, "La URL pública previamente conocida sigue funcionando");
        }
        finally
        {
            fabrica.Services.GetRequiredService<IAlmacenImagenes>().IntentarEliminar(ultima.NombreArchivo);
        }

        Autorizar(cliente, tokenAdmin);
        var movimientos = await cliente.GetFromJsonAsync<List<MovimientoAuditoriaRespuesta>>("/api/auditoria/movimientos?entidad=Imagen");
        Verificar(movimientos?.Select(m => m.Accion).SequenceEqual(new[] { "SubirImagen", "EliminarImagen", "ReemplazarImagen", "SubirImagen" }) == true,
            "Cada cambio de imagen confirmado queda auditado");
    }
}
