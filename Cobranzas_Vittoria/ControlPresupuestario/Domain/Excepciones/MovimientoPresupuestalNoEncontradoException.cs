namespace Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;

public sealed class MovimientoPresupuestalNoEncontradoException : RecursoPresupuestarioNoEncontradoException
{
    public const string Codigo = "MOVIMIENTO_NO_ENCONTRADO";

    public MovimientoPresupuestalNoEncontradoException(long id) : base(Codigo, $"El movimiento {id} no existe.") { }
}
