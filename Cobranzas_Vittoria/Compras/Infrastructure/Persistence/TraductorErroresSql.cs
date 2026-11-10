using Cobranzas_Vittoria.Compras.Domain.Excepciones;
using Microsoft.Data.SqlClient;

namespace Cobranzas_Vittoria.Compras.Infrastructure.Persistence;

/// <summary>Traduce contratos SQL conocidos por operación; no oculta fallos técnicos.</summary>
internal static class TraductorErroresSql
{
    public static async Task<T> EjecutarCreacionAsync<T>(Func<Task<T>> accion)
    {
        try
        {
            return await accion();
        }
        catch (SqlException ex) when (ex.Number is 51420 or >= 51470 and <= 51475)
        {
            string[] partes = ex.Message.Split(':', 2);
            string codigo = partes[0].Trim();
            string mensaje = partes.Length == 2 ? partes[1].Trim() : "La creación del requerimiento fue rechazada.";

            throw ex.Number switch
            {
                51470 => new ValidacionComprasException(codigo, mensaje),
                51471 => new NumeroRequerimientoOcupadoException(),
                51472 => new RecursoComprasNoEncontradoException(codigo, mensaje),
                _ => new ConflictoComprasException(codigo, mensaje)
            };
        }
    }

    public static async Task<T> EjecutarAsync<T>(Func<Task<T>> accion)
    {
        try
        {
            return await accion();
        }
        catch (SqlException ex) when (ex.Number is >= 51460 and <= 51469)
        {
            string[] partes = ex.Message.Split(':', 2);
            string codigo = partes[0].Trim();
            string mensaje = partes.Length == 2 ? partes[1].Trim() : "La operación de Compras fue rechazada.";

            throw ex.Number switch
            {
                51460 => new ValidacionComprasException(codigo, mensaje),
                51461 or 51464 => new RecursoComprasNoEncontradoException(codigo, mensaje),
                _ => new ConflictoComprasException(codigo, mensaje)
            };
        }
    }
}
