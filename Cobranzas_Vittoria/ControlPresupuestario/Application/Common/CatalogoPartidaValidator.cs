using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.Common;

public static class CatalogoPartidaValidator
{
    public static void ValidarId(int idCatalogoPartida) => Validacion.Id(idCatalogoPartida, "IdCatalogoPartida");

    public static void ValidarFiltro(bool soloRaices, int? idPartidaPadre)
    {
        if (soloRaices && idPartidaPadre.HasValue)
            throw new ValidacionPresupuestariaException("FILTRO_INVALIDO", "No se puede combinar soloRaices con idPartidaPadre.");
        Validacion.IdOpcional(idPartidaPadre, "IdPartidaPadre");
    }
}
