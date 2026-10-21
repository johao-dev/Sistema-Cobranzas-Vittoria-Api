using Cobranzas_Vittoria.Application.Common;
using Cobranzas_Vittoria.Application.Common.Excepciones;
using Cobranzas_Vittoria.Inventario.Application.Excepciones;
using Cobranzas_Vittoria.Inventario.Domain.Excepciones;
using Cobranzas_Vittoria.Inventario.Domain.Model;
using Microsoft.Data.SqlClient;

namespace Cobranzas_Vittoria.Inventario.Infrastructure.Persistence;

/// <summary>Traduce los rechazos del rango SQL de Inventario y propaga los fallos técnicos.</summary>
internal static class TraductorErroresSql
{
    public static async Task<T> EjecutarAsync<T>(Func<Task<T>> operacion, string tipoKardex)
    {
        try
        {
            return await operacion();
        }
        catch (SqlException ex) when (SqlExceptionTranslator.Traducir(ex) is { } traduccion)
        {
            if (traduccion.CodigoError == CodigosErrorInventario.Sp.KardexNoEncontrado)
            {
                throw new KardexNoEncontradoException(tipoKardex, idKardex: 0);
            }

            DetalleErrorValidacion detalle = new(
                Fila: traduccion.Fila,
                Campo: string.Empty,
                CodigoError: traduccion.CodigoError,
                Mensaje: traduccion.Mensaje);

            throw new ValidacionNegocioInventarioException(new[] { detalle });
        }
    }

    public static async Task EjecutarAsync(Func<Task> operacion, string tipoKardex)
    {
        await EjecutarAsync<object?>(async () =>
        {
            await operacion();
            return null;
        }, tipoKardex);
    }
}
