using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using turnero_peluqueria.Services;

namespace turnero_peluqueria.Controllers;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class LimiteCuerpoImagenAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext contexto)
    {
        if (contexto.HttpContext.Request.ContentLength > ImagenPerroService.MaxPeticion)
        {
            contexto.Result = new ObjectResult(new { error = "El cuerpo de la solicitud de imagen supera el tamaño permitido." })
            {
                StatusCode = StatusCodes.Status413PayloadTooLarge
            };
        }
    }

    public void OnResourceExecuted(ResourceExecutedContext contexto) { }
}
