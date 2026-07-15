namespace Cobranzas_Vittoria.Application.Importacion;

/// <summary>
/// Catalogo centralizado de codigos de error que devuelve la API en el campo
/// <c>codigoError</c> de cada <c>DetalleErrorFila</c>.
///
/// Se modela como <c>static class</c> anidada (no como <c>enum</c>) porque:
///   - los codigos son cadenas de texto que la API expone en JSON y
///     consumidores externos pueden depender literalmente de ellas;
///   - agruparlas por categoria (Estructura, Fila, Sp) reduce el acoplamiento
///     entre las capas que solo consumen un subconjunto;
///   - <c>const string</c> permite que el compilador los inline sin alocar
///     objetos y que herramientas de refactor los renombren con seguridad.
///
/// CUALQUIER cambio aqui es un breaking change de API publica.
/// </summary>
public static class CodigosError
{
    /// <summary>Errores relacionados con la estructura del archivo (encabezados, cantidad de filas).</summary>
    public static class Estructura
    {
        public const string EncabezadosIncorrectos = "ENCABEZADOS_INCORRECTOS";
        public const string DemasiadasFilas = "DEMASIADAS_FILAS";
        public const string ArchivoSinDatos = "ARCHIVO_SIN_DATOS";
    }

    /// <summary>Errores de una fila especifica, originados en la capa de aplicacion (no en el SP).</summary>
    public static class Fila
    {
        public const string CampoRequerido = "CAMPO_REQUERIDO";
        public const string FormatoInvalido = "FORMATO_INVALIDO";
        public const string ReglaNegocio = "REGLA_NEGOCIO";
    }

    /// <summary>Errores que devuelve el Stored Procedure (codigos SQL 50001-50099).</summary>
    public static class Sp
    {
        public const string CampoObligatorio = "CAMPO_OBLIGATORIO";
        public const string ValorDuplicadoEnArchivo = "VALOR_DUPLICADO_EN_ARCHIVO";
        public const string ValorYaExisteEnBd = "VALOR_YA_EXISTE_EN_BD";
        public const string FkNoExiste = "FK_NO_EXISTE";

        /// <summary>Prefijo para errores de validacion fuera de 50001-50004. El numero del SP se concatena con '_'.</summary>
        public const string ErrorValidacionPrefijo = "ERROR_VALIDACION";
    }
}
