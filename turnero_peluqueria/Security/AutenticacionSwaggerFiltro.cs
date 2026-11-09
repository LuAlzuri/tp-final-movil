using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace turnero_peluqueria.Security;

internal sealed class AutenticacionSwaggerFiltro : IOperationFilter
{
    public void Apply(OpenApiOperation operacion, OperationFilterContext contexto)
    {
        if (contexto.ApiDescription.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
            operacion.Security = [];
    }
}
