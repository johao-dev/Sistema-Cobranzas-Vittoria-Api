namespace Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;

public sealed class CatalogoPartidaNoEncontradoException : RecursoPresupuestarioNoEncontradoException
{
    public const string Codigo = "PARTIDA_NO_ENCONTRADA";

    public CatalogoPartidaNoEncontradoException(int id) : base(Codigo, $"La partida {id} no existe.") { }
}
