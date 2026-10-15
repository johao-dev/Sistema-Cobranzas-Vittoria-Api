using Cobranzas_Vittoria.Compras.Domain.Excepciones;
using Microsoft.Data.SqlClient;

namespace Cobranzas_Vittoria.Compras.Infrastructure.Persistence;

/// <summary>Solo traduce el contrato SQL de ítems (51460–51469); no oculta fallos técnicos.</summary>
internal static class TraductorErroresSql
{
    public static async Task<T> EjecutarAsync<T>(Func<Task<T>> accion)
    {
        try { return await accion(); }
        catch (SqlException ex) when (ex.Number is >= 51460 and <= 51469)
        {
            var partes = ex.Message.Split(':', 2);
            var codigo = partes[0].Trim();
            var mensaje = partes.Length == 2 ? partes[1].Trim() : "La operación de Compras fue rechazada.";
            throw ex.Number switch
            {
                51460 => new ValidacionComprasException(codigo, mensaje),
                51461 or 51464 => new RecursoComprasNoEncontradoException(codigo, mensaje),
                _ => new ConflictoComprasException(codigo, mensaje)
            };
        }
    }
}
