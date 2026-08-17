namespace Cobranzas_Vittoria.Application.Inventario;

/// <summary>
/// Catalogo centralizado de codigos de error que devuelve el modulo Inventario
/// (Kardex manual) en el campo <c>codigoError</c> de cada <c>DetalleErrorFila</c>.
///
/// <para>
/// <b>Convencion</b>: estos codigos coinciden EXACTAMENTE con los prefijos
/// que emiten los SPs de <c>almacen.usp_Kardex*</c> en el formato
/// <c>'CODIGO: detalle'</c> (ver <c>Application/Common/SqlExceptionTranslator</c>).
/// Si se renombra un codigo aqui, hay que renombrarlo tambien en el SP
/// correspondiente (y viceversa) para mantener la coherencia.
/// </para>
///
/// <para>
/// <b>Por que <c>static class</c> anidada (no <c>enum</c>)</b>:
///   - los codigos son cadenas que la API expone en JSON y consumidores
///     externos (frontend, integraciones) pueden depender literalmente;
///   - agruparlos por categoria (Validacion, Stock, Sp) reduce el acoplamiento
///     entre las capas que solo consumen un subconjunto;
///   - <c>const string</c> permite que el compilador los inline y que
///     herramientas de refactor los renombren con seguridad.
/// </para>
///
/// CUALQUIER cambio aqui es un breaking change de API publica.
/// </summary>
public static class CodigosErrorInventario
{
    /// <summary>
    /// Errores generados por validacion a nivel de API (no en el SP).
    /// Se usan cuando el validador de Inventario detecta un problema
    /// antes de invocar la base de datos.
    /// </summary>
    public static class Validacion
    {
        /// <summary>Campo requerido ausente o vacio (mapea a 51100 del SP).</summary>
        public const string CampoRequerido = "CAMPO_REQUERIDO";

        /// <summary>Cantidad con valor invalido (negativo, cero donde se requiere positivo, etc).</summary>
        public const string CantidadInvalida = "CANTIDAD_INVALIDA";

        /// <summary>FK a maestra.* inexistente o inactiva.</summary>
        public const string FkNoExiste = "FK_NO_EXISTE";

        /// <summary>Lista de items vacia o con un item invalido.</summary>
        public const string ItemsInvalidos = "ITEMS_INVALIDOS";
    }

    /// <summary>
    /// Errores especificos de la logica de stock (rangos 51110-51119 del SP).
    /// Son los que KardexInventarioValidator y el service mapean
    /// antes de invocar al SP.
    /// </summary>
    public static class Stock
    {
        /// <summary>La salida solicitada supera el stock disponible (mapea a 51110 del SP).</summary>
        public const string StockInsuficiente = "STOCK_INSUFICIENTE";

        /// <summary>
        /// La eliminacion de una entrada dejaria el stock negativo porque
        /// hay salidas posteriores que dependen de esa entrada (mapea a 51111).
        /// </summary>
        public const string StockInconsistenteAlEliminar = "STOCK_INCONSISTENTE_AL_ELIMINAR";
    }

    /// <summary>
    /// Errores que devuelve el Stored Procedure directamente
    /// (rangos SQL 51100-51199). Se preservan tal cual para que el
    /// cliente sepa que la validacion fue del lado de la BD.
    /// </summary>
    public static class Sp
    {
        /// <summary>Prefijo para errores 51100-51199 no contemplados explicitamente arriba.</summary>
        public const string ErrorValidacionPrefijo = "ERROR_VALIDACION";

        /// <summary>Kardex no encontrado por Id (mapea a 51104 del SP).</summary>
        public const string KardexNoEncontrado = "KARDEX_NO_ENCONTRADO";
    }

    /// <summary>
    /// Errores generados por la propia API de Inventario (no del SP, no de
    /// validacion de negocio). Son equivalentes a los del modulo Importacion
    /// pero viven en este namespace para evitar colision y para que Inventario
    /// pueda evolucionar su contrato independientemente.
    /// </summary>
    public static class Api
    {
        /// <summary>El id de la ruta no coincide con el id del cuerpo (PUT /:id con body.id distinto).</summary>
        public const string IdRutaInconsistente = "ID_RUTA_INCONSISTENTE";

        /// <summary>El kardex solicitado no existe (404). Distinto de KARDEX_NO_ENCONTRADO que es del SP.</summary>
        public const string RecursoNoEncontrado = "RECURSO_NO_ENCONTRADO";
    }
}
