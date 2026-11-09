using System.Text.RegularExpressions;

namespace turnero_peluqueria.Services;

public interface IAlmacenImagenes
{
    Task GuardarAsync(string nombre, byte[] contenido, CancellationToken cancellationToken);
    void IntentarEliminar(string nombre);
}

public sealed class AlmacenImagenes(IWebHostEnvironment environment, ILogger<AlmacenImagenes> logger) : IAlmacenImagenes
{
    private readonly string directorio = Path.Combine(environment.WebRootPath, "uploads");

    private string Ruta(string nombre)
    {
        if (!Regex.IsMatch(nombre, @"\A[0-9a-f]{32}\.(jpg|png|webp)\z", RegexOptions.CultureInvariant))
            throw new ArgumentException("Nombre de imagen no administrado.", nameof(nombre));
        return Path.Combine(directorio, nombre);
    }

    public async Task GuardarAsync(string nombre, byte[] contenido, CancellationToken cancellationToken)
    {
        var ruta = Ruta(nombre);
        Directory.CreateDirectory(directorio);
        var creado = false;
        try
        {
            await using var stream = new FileStream(ruta, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                81920, FileOptions.Asynchronous);
            creado = true;
            await stream.WriteAsync(contenido, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
        catch
        {
            if (creado) IntentarEliminar(nombre);
            throw;
        }
    }

    public void IntentarEliminar(string nombre)
    {
        try { File.Delete(Ruta(nombre)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // La limpieza no revierte metadatos confirmados ni toca rutas ajenas.
            logger.LogWarning("No se pudo limpiar una imagen anterior o temporal; revisar el almacenamiento.");
        }
    }
}
