namespace turnero_peluqueria.Exceptions;

public sealed class ApiException(string mensaje, int codigoEstado = StatusCodes.Status400BadRequest)
    : Exception(mensaje)
{
    public int CodigoEstado { get; } = codigoEstado;
}
