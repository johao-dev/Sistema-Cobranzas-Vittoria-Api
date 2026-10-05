using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Excepciones;

namespace Cobranzas_Vittoria.Contable.GastosDirectos.Application;

public static class GastoDirectoValidator
{
    public static void ValidarId(int idGastoDirecto)
    {
        if (idGastoDirecto <= 0) throw new ValidacionGastoDirectoException("El identificador del gasto debe ser positivo.");
    }
}
