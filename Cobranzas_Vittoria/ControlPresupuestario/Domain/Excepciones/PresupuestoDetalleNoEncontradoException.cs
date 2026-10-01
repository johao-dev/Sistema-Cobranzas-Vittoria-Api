namespace Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;

public sealed class PresupuestoDetalleNoEncontradoException : RecursoPresupuestarioNoEncontradoException
{
    public const string Codigo = "PARTIDA_DE_VERSION_NO_ENCONTRADA";

    public PresupuestoDetalleNoEncontradoException(int id) : base(Codigo, $"La partida {id} no existe en la versión indicada.") { }
}
