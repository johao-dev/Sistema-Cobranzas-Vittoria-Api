namespace Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;

/// <summary>Recurso del módulo inexistente (HTTP 404).</summary>
public class RecursoPresupuestarioNoEncontradoException : ControlPresupuestarioException
{
    public RecursoPresupuestarioNoEncontradoException(string codigoError, string mensaje) : base(codigoError, mensaje) { }
}
