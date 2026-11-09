using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace turnero_peluqueria.DTOs;

public sealed record ImagenResponse(int Id, int PerroId, string NombreArchivo, string ImagenUrl,
    string TipoContenido, long TamanoBytes);

public sealed class ImagenSubidaRequest
{
    [FromForm(Name = "archivo")]
    [Required(ErrorMessage = "Adjuntá una imagen en el campo archivo.")]
    public IFormFile Archivo { get; init; } = null!;
}
