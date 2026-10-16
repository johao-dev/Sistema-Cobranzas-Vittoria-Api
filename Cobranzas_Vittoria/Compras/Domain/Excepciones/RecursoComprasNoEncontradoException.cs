namespace Cobranzas_Vittoria.Compras.Domain.Excepciones;

/// <summary>Base de los errores por recursos inexistentes en el contexto solicitado.</summary>
public class RecursoComprasNoEncontradoException : ComprasException
{
    public RecursoComprasNoEncontradoException(string codigoError, string mensaje)
        : base(codigoError, mensaje) { }
}
