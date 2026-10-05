namespace Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;

public sealed class PresupuestoNoEncontradoException : RecursoPresupuestarioNoEncontradoException
{
    public const string Codigo = "PRESUPUESTO_NO_ENCONTRADO";

    public PresupuestoNoEncontradoException(int id) : base(Codigo, $"El presupuesto {id} no existe.") { }
}
