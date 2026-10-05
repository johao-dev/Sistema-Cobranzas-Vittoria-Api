namespace Cobranzas_Vittoria.ControlPresupuestario.Application.Common;

public static class CentroCostoValidator
{
    public static void ValidarId(int idCentroCosto) => Validacion.Id(idCentroCosto, "IdCentroCosto");

    public static void ValidarFiltro(int? idTipoCentroCosto, int? idProyecto)
    {
        Validacion.IdOpcional(idTipoCentroCosto, "IdTipoCentroCosto");
        Validacion.IdOpcional(idProyecto, "IdProyecto");
    }
}
