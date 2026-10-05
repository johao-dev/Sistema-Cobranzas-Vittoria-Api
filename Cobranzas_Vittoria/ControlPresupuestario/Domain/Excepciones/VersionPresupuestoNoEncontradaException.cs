namespace Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;

public sealed class VersionPresupuestoNoEncontradaException : RecursoPresupuestarioNoEncontradoException
{
    public const string Codigo = "VERSION_NO_ENCONTRADA";

    public VersionPresupuestoNoEncontradaException(int id) : base(Codigo, $"La versión {id} no existe en el presupuesto indicado.") { }
}
