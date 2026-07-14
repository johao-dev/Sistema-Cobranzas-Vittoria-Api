using System.Data;

namespace Cobranzas_Vittoria.Application.Importacion.Persistence;

/// <summary>
/// Contrato generico para la persistencia de cargas masivas via SP + TVP.
///
/// Cada modulo de mantenimiento (UnidadMedida, CategoriaGasto, etc.) define:
///   - Un TVP en una migracion versionada (ej. <c>maestra.TVP_UnidadMedida</c>).
///   - Un SP con la logica de validacion + insert (ej. <c>maestra.usp_UnidadMedida_CargaMasiva</c>).
///   - Un DTO cuyas propiedades publicas coinciden 1-a-1 con las columnas del TVP.
///
/// El metodo <see cref="ImportAsync{TDto}"/> recibe el nombre del SP y del TVP
/// como parametros para evitar un metodo por modulo (romperia la arquitectura).
/// La conversion de <c>IEnumerable&lt;TDto&gt;</c> a <see cref="DataTable"/> la hace
/// <see cref="TvpMapper"/> internamente.
///
/// <para>
/// <b>Parametros extra del SP:</b> si el SP requiere parametros adicionales (ej. <c>@Usuario</c>),
/// paselos en <paramref name="extraParameters"/> como objeto anonimo. Cada propiedad
/// publica del objeto se agrega como parametro del SP. Use <c>null</c> si no hay extras.
/// </para>
///
/// <para>
/// <b>Transaccion:</b> el SP controla su propia transaccion interna. Si pasa
/// <paramref name="transaction"/> no nula, se incluye en la llamada al SP para que
/// pueda formar parte de una transaccion mayor del caller.
/// </para>
///
/// <para>
/// <b>Errores:</b> si el SP lanza <see cref="Microsoft.Data.SqlClient.SqlException"/>,
/// el repository lo propaga sin modificar. El <c>ImportService</c> (Fase 4) se
/// encarga de traducirlo a <c>DatosInvalidosException</c>.
/// </para>
/// </summary>
public interface IImportRepository
{
    /// <summary>
    /// Ejecuta el SP de carga masiva con el TVP construido a partir de <paramref name="dtos"/>.
    /// </summary>
    /// <param name="spName">Nombre completo del SP (ej. "maestra.usp_UnidadMedida_CargaMasiva").</param>
    /// <param name="tvpTypeName">Nombre completo del TVP (ej. "maestra.TVP_UnidadMedida").</param>
    /// <param name="dtos">Filas a insertar. La lista puede estar vacia (no se llama al SP).</param>
    /// <param name="connection">Conexion abierta a SQL Server.</param>
    /// <param name="transaction">Transaccion del caller, o <c>null</c> si el SP maneja la suya.</param>
    /// <param name="extraParameters">
    /// Parametros adicionales del SP como objeto anonimo (ej. <c>new { Usuario = "juan" }</c>),
    /// o <c>null</c> si no hay extras.
    /// </param>
    /// <param name="ct">Token de cancelacion.</param>
    /// <returns>Cantidad de filas afectadas (las insertadas por el SP).</returns>
    Task<int> ImportAsync<TDto>(
        string spName,
        string tvpTypeName,
        IEnumerable<TDto> dtos,
        IDbConnection connection,
        IDbTransaction? transaction,
        object? extraParameters = null,
        CancellationToken ct = default) where TDto : class;
}
