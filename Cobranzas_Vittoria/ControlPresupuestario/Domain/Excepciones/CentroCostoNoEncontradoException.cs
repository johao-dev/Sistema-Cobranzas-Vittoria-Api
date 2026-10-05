namespace Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;

public sealed class CentroCostoNoEncontradoException : RecursoPresupuestarioNoEncontradoException
{
    public const string Codigo = "CENTRO_COSTO_NO_ENCONTRADO";

    public CentroCostoNoEncontradoException(int id) : base(Codigo, $"El centro de costo {id} no existe.") { }
}
