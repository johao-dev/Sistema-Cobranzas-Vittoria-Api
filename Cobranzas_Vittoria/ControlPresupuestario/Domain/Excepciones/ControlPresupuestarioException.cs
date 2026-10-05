namespace Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;

/// <summary>
/// Regla de negocio presupuestaria rechazada (HTTP 409). Es la base de todas las
/// excepciones del módulo: el middleware distingue las subclases de validación (400)
/// y de recurso inexistente (404). CodigoError viaja tal cual al cliente.
/// </summary>
public class ControlPresupuestarioException : Exception
{
    public string CodigoError { get; }

    public ControlPresupuestarioException(string codigoError, string mensaje)
        : base(mensaje) => CodigoError = codigoError;
}
