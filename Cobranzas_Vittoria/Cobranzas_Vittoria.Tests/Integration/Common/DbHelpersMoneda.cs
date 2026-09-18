namespace Cobranzas_Vittoria.Tests.Integration.Common;

internal static class DbHelpersMoneda
{
    public static Task<int> ObtenerPenAsync() => DbHelpers.QueryScalarAsync<int>(
        "SELECT IdMoneda FROM maestra.Moneda WHERE Codigo = 'PEN'");
}
