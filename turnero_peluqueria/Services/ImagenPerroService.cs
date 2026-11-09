using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;
using turnero_peluqueria.Data;
using turnero_peluqueria.DTOs;
using turnero_peluqueria.Entities;
using turnero_peluqueria.Exceptions;

namespace turnero_peluqueria.Services;

public sealed class ImagenPerroService(AppDbContext db, IAlmacenImagenes almacen,
    AuditoriaAccionesService auditoria, ILogger<ImagenPerroService> logger)
{
    public const int MaxTamano = 5 * 1024 * 1024;
    public const int MaxPeticion = MaxTamano + 64 * 1024;
    public const long MaxPixeles = 20_000_000;

    public async Task<ImagenResponse> GuardarAsync(int perroId, IFormFile archivo, CancellationToken ct)
    {
        if (!await db.Perros.AnyAsync(p => p.Id == perroId && !p.Eliminado, ct))
            throw PerroNoEncontrado();

        var (contenido, extension, mime) = await ValidarAsync(archivo, ct);
        var nombre = Guid.NewGuid().ToString("N") + extension;
        await almacen.GuardarAsync(nombre, contenido, ct);

        var commitIntentado = false;
        string? anterior = null;
        Imagen imagen;
        try
        {
            await using var transaccion = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await BloquearPerroActivoAsync(perroId, ct);
            imagen = await db.Imagenes.SingleOrDefaultAsync(i => i.PerroId == perroId, ct)
                ?? new Imagen { PerroId = perroId };
            anterior = imagen.Id == 0 ? null : imagen.NombreArchivo;
            if (imagen.Id == 0)
                db.Imagenes.Add(imagen);

            imagen.NombreArchivo = nombre;
            imagen.RutaRelativa = "/uploads/" + nombre;
            imagen.TipoContenido = mime;
            imagen.TamanoBytes = contenido.LongLength;
            await db.SaveChangesAsync(ct);
            auditoria.Registrar(anterior is null ? "SubirImagen" : "ReemplazarImagen", nameof(Imagen), imagen.Id);
            await db.SaveChangesAsync(ct);
            commitIntentado = true;
            await transaccion.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            if (commitIntentado)
                logger.LogWarning("Confirmación de imagen con resultado incierto. Se conservan los archivos para revisión.");
            else
                almacen.IntentarEliminar(nombre);
            if (!commitIntentado && EsConflictoSql(ex))
                throw Conflicto();
            throw;
        }

        if (anterior is not null)
            almacen.IntentarEliminar(anterior);

        return new ImagenResponse(imagen.Id, perroId, imagen.NombreArchivo, imagen.RutaRelativa,
            imagen.TipoContenido, imagen.TamanoBytes);
    }

    public async Task EliminarAsync(int perroId, CancellationToken ct)
    {
        var commitIntentado = false;
        string nombre;
        try
        {
            await using var transaccion = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await BloquearPerroActivoAsync(perroId, ct);
            var imagen = await db.Imagenes.SingleOrDefaultAsync(i => i.PerroId == perroId, ct)
                ?? throw new ApiException("El perro no tiene una imagen.", StatusCodes.Status404NotFound);
            nombre = imagen.NombreArchivo;
            db.Imagenes.Remove(imagen);
            auditoria.Registrar("EliminarImagen", nameof(Imagen), imagen.Id);
            await db.SaveChangesAsync(ct);
            commitIntentado = true;
            await transaccion.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            if (commitIntentado)
                logger.LogWarning("Confirmación del borrado de imagen con resultado incierto. Se conserva el archivo para revisión.");
            if (!commitIntentado && EsConflictoSql(ex))
                throw Conflicto();
            throw;
        }

        almacen.IntentarEliminar(nombre);
    }

    private async Task BloquearPerroActivoAsync(int perroId, CancellationToken ct)
    {
        // La escritura condicional retiene el bloqueo de la fila hasta confirmar.
        var afectados = await db.Perros.Where(p => p.Id == perroId && !p.Eliminado)
            .ExecuteUpdateAsync(cambios => cambios.SetProperty(p => p.Eliminado, p => p.Eliminado), ct);
        if (afectados != 1)
            throw PerroNoEncontrado();
    }

    private static bool EsConflictoSql(Exception ex) =>
        ex.GetBaseException() is SqlException { Number: 1205 or 1222 or 2601 or 2627 or 3960 };

    private static ApiException Conflicto() =>
        new("Hubo una modificación simultánea. Volvé a intentar la operación.", StatusCodes.Status409Conflict);

    private static ApiException PerroNoEncontrado() =>
        new("No se encontró el perro.", StatusCodes.Status404NotFound);

    private static async Task<(byte[] Contenido, string Extension, string Mime)> ValidarAsync(IFormFile archivo, CancellationToken ct)
    {
        if (archivo.Length <= 0) throw new ApiException("Debe adjuntar una imagen no vacía.");
        if (archivo.Length > MaxTamano) throw new ApiException("La imagen supera 5 MiB.", StatusCodes.Status413PayloadTooLarge);
        var extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
        var expected = extension switch
        {
            ".jpg" or ".jpeg" => (SKEncodedImageFormat.Jpeg, "image/jpeg", ".jpg"),
            ".png" => (SKEncodedImageFormat.Png, "image/png", ".png"),
            ".webp" => (SKEncodedImageFormat.Webp, "image/webp", ".webp"),
            _ => throw new ApiException("Use una imagen JPG, PNG o WebP.")
        };
        if (!string.Equals(archivo.ContentType, expected.Item2, StringComparison.OrdinalIgnoreCase))
            throw new ApiException("El tipo de contenido no coincide con la imagen.");

        await using var input = archivo.OpenReadStream();
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int count;
        while ((count = await input.ReadAsync(chunk, ct)) != 0)
        {
            if (buffer.Length + count > MaxTamano)
                throw new ApiException("La imagen supera 5 MiB.", StatusCodes.Status413PayloadTooLarge);
            buffer.Write(chunk, 0, count);
        }
        var bytes = buffer.ToArray();
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        if (codec is null || codec.EncodedFormat != expected.Item1)
            throw new ApiException("El archivo no contiene una imagen válida del formato indicado.");
        var source = codec.Info;
        if (source.Width <= 0 || source.Height <= 0 || source.Width > 8192 || source.Height > 8192
            || (long)source.Width * source.Height > MaxPixeles || codec.FrameCount > 1)
            throw new ApiException("La imagen debe ser estática, de hasta 8192 píxeles por lado y 20 megapíxeles.");
        using var bitmap = new SKBitmap(new SKImageInfo(source.Width, source.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
        if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
            throw new ApiException("La imagen está dañada o incompleta.");
        return (bytes, expected.Item3, expected.Item2);
    }
}
