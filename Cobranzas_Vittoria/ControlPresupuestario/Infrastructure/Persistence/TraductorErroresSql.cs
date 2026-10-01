using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;
using Microsoft.Data.SqlClient;

namespace Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence;

/// <summary>
/// Traduce los rechazos de los SPs del módulo (rango 51200-51299, formato 'CODIGO: detalle') a
/// excepciones de dominio, para que ninguna capa superior dependa de SqlException. La API no
/// reinterpreta la regla: conserva el código y el mensaje que emite SQL Server.
/// </summary>
internal static class TraductorErroresSql
{
    private const int ContratoInvalido = 51200;
    private const int ReferenciaInexistente = 51201;
    private const int RecursoInexistente = 51203;

    public static async Task<T> EjecutarAsync<T>(Func<Task<T>> accion)
    {
        try { return await accion(); }
        catch (SqlException ex) when (EsDelModulo(ex)) { throw Traducir(ex); }
    }

    public static async Task EjecutarAsync(Func<Task> accion)
    {
        try { await accion(); }
        catch (SqlException ex) when (EsDelModulo(ex)) { throw Traducir(ex); }
    }

    private static bool EsDelModulo(SqlException ex) => ex.Number is >= 51200 and <= 51299;

    internal static ControlPresupuestarioException Traducir(SqlException ex) => Traducir(ex.Number, ex.Message);

    internal static ControlPresupuestarioException Traducir(int numero, string mensajeSql)
    {
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
