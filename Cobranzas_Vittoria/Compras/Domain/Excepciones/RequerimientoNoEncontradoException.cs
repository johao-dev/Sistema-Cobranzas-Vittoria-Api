namespace Cobranzas_Vittoria.Compras.Domain.Excepciones;

/// <summary>El requerimiento solicitado no existe.</summary>
public sealed class RequerimientoNoEncontradoException : RecursoComprasNoEncontradoException
{
    public const string Codigo = "REQUERIMIENTO_NO_ENCONTRADO";

    public RequerimientoNoEncontradoException(int idRequerimiento)
        : base(Codigo, $"El requerimiento {idRequerimiento} no existe.") { }
}
