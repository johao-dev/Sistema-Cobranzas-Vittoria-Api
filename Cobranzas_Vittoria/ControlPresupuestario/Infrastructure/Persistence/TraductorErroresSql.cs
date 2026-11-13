using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;
using Microsoft.Data.SqlClient;

namespace Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence;

/// <summary>
/// Traduce los rechazos de los SPs del módulo (rango 51200-51299, formato 'CODIGO: detalle') y
/// las víctimas de interbloqueo a excepciones de dominio. Los conflictos de concurrencia
/// tienen un mensaje propio; los rechazos de negocio conservan el código y mensaje del SP.
/// </summary>
internal static class TraductorErroresSql
{
    private const int ContratoInvalido = 51200;
    private const int ReferenciaInexistente = 51201;
    private const int RecursoInexistente = 51203;
    private const int INTERBLOQUEO = 1205;

    public static async Task<T> EjecutarAsync<T>(Func<Task<T>> accion)
    {
        try { return await accion(); }
        catch (SqlException ex) when (EsTraducible(ex)) { throw Traducir(ex); }
    }

    public static async Task EjecutarAsync(Func<Task> accion)
    {
        try { await accion(); }
        catch (SqlException ex) when (EsTraducible(ex)) { throw Traducir(ex); }
    }

    private static bool EsTraducible(SqlException ex) => ex.Number is INTERBLOQUEO or >= 51200 and <= 51299;

    internal static ControlPresupuestarioException Traducir(SqlException ex) => Traducir(ex.Number, ex.Message);

    internal static ControlPresupuestarioException Traducir(int numero, string mensajeSql)
    {
        if (numero == INTERBLOQUEO)
        {
            return new ControlPresupuestarioException("CONFLICTO_CONCURRENCIA",
                "La operación entró en conflicto con otra operación simultánea. Vuelve a intentarlo.");
        }

        var separador = mensajeSql.IndexOf(':');
        var codigo = separador > 0 ? mensajeSql[..separador].Trim() : "CONFLICTO_NEGOCIO_PRESUPUESTARIO";
        var mensaje = separador > 0 ? mensajeSql[(separador + 1)..].Trim() : "La operación presupuestaria fue rechazada.";
        return numero switch
        {
            ContratoInvalido or ReferenciaInexistente => new ValidacionPresupuestariaException(codigo, mensaje),
            RecursoInexistente => new RecursoPresupuestarioNoEncontradoException(codigo, mensaje),
            _ => new ControlPresupuestarioException(codigo, mensaje)
        };
    }
}
