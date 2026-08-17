using Microsoft.Data.SqlClient;

namespace Cobranzas_Vittoria.Application.Common;

/// <summary>
/// Helper estatico y sin estado para traducir <see cref="SqlException"/>
/// lanzadas por los Stored Procedures del modulo Inventario a una tupla
/// (CodigoError, Mensaje, Fila) consumible por la capa de aplicacion.
///
/// <para>
/// <b>Diseno desacoplado</b>: este helper NO lanza ni construye ninguna
/// excepcion propia. Devuelve un <see cref="ResultadoTraduccionSql"/>
/// inmutable para que cada modulo (Inventario y futuros) decida como
/// envolverlo segun su jerarquia de excepciones. Esto evita el acoplamiento
/// de <c>Application.Common</c> con <c>Application.Importacion.Excepciones</c>
/// u otros modulos.
/// </para>
///
/// <para>
/// <b>Convencion de mensaje del SP</b>: el SP emite el mensaje con el
/// formato <c>'CODIGO: detalle'</c> (sin espacios alrededor del <c>:</c>).
/// Si el mensaje cumple ese formato, se usa el codigo del prefijo; si no,
/// se usa el nombre del SP (o el numero crudo como ultimo recurso). Esto
/// permite que el SP reporte errores especificos (ej: 51110 STOCK_INSUFICIENTE)
/// y que el backend los mapee a un codigo legible sin parseos fragiles.
/// </para>
///
/// <para>
/// <b>Rango cubierto</b>: 51100-51199 (reservado para el modulo Inventario).
/// Otros rangos quedan fuera del alcance de este helper. El caller debe
/// decidir el flujo por defecto para numeros fuera de rango.
/// </para>
/// </summary>
public static class SqlExceptionTranslator
{
    /// <summary>Inicio del rango reservado para el modulo Inventario (Kardex manual).</summary>
    public const int RangoInventarioInicio = 51100;

    /// <summary>Fin del rango reservado para el modulo Inventario (Kardex manual).</summary>
    public const int RangoInventarioFin = 51199;

    /// <summary>
    /// Traduce una <see cref="SqlException"/> del rango Inventario a un
    /// <see cref="ResultadoTraduccionSql"/> inmutable. Si el numero esta
    /// fuera del rango 51100-51199, devuelve <c>null</c> y el caller decide
    /// que hacer (normalmente re-lanzar la excepcion o mapear a 500).
    /// </summary>
    /// <param name="ex">Excepcion SQL capturada.</param>
    /// <returns>Tupla con codigo, mensaje y fila, o <c>null</c> si no aplica.</returns>
    public static ResultadoTraduccionSql? Traducir(SqlException ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        if (ex.Number is < RangoInventarioInicio or > RangoInventarioFin)
        {
            return null;
        }

        var (codigo, mensaje) = ParsearMensaje(ex.Message);
        return new ResultadoTraduccionSql(
            CodigoError: codigo,
            Mensaje: mensaje,
            Fila: 0, // Los SPs de Inventario no reportan fila (mejora futura).
            NumeroSql: ex.Number);
    }

    /// <summary>
    /// Parsea el mensaje del SP con el formato <c>'CODIGO: detalle'</c>.
    /// Si no hay separador <c>:</c>, usa el mensaje completo como detalle
    /// y devuelve un codigo derivado del numero de error.
    /// </summary>
    private static (string Codigo, string Mensaje) ParsearMensaje(string mensajeOriginal)
    {
        if (string.IsNullOrWhiteSpace(mensajeOriginal))
        {
            return ("ERROR_SQL_VACIO", "El Stored Procedure no devolvio mensaje de error.");
        }

        var idx = mensajeOriginal.IndexOf(':');
        if (idx <= 0)
        {
            // Sin prefijo CODIGO: se usa el mensaje completo como detalle
            // y un codigo generico derivado del numero.
            return ("ERROR_VALIDACION", mensajeOriginal.Trim());
        }

        var codigoCrudo = mensajeOriginal[..idx].Trim();
        var detalle = mensajeOriginal[(idx + 1)..].Trim();

        // Si el prefijo es solo espacios o vacio, no es un codigo valido.
        if (string.IsNullOrWhiteSpace(codigoCrudo))
        {
            return ("ERROR_VALIDACION", mensajeOriginal.Trim());
        }

        return (codigoCrudo, detalle);
    }
}

/// <summary>
/// Resultado inmutable de traducir una <see cref="SqlException"/>.
/// El caller lo envuelve en la excepcion de su modulo (ej:
/// <c>ValidacionNegocioInventarioException</c>) preservando el contrato
/// HTTP del middleware.
/// </summary>
/// <param name="CodigoError">Codigo legible (ej: <c>"STOCK_INSUFICIENTE"</c>).</param>
/// <param name="Mensaje">Detalle legible para mostrar al cliente.</param>
/// <param name="Fila">Numero de fila (0 si el SP no lo reporta).</param>
/// <param name="NumeroSql">Numero del error SQL original (50001, 51110, etc).</param>
public sealed record ResultadoTraduccionSql(
    string CodigoError,
    string Mensaje,
    int Fila,
    int NumeroSql);
