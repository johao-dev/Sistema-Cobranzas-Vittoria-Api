using System.Data;
using Cobranzas_Vittoria.Application.Importacion.Persistence;
using Dapper;

namespace Cobranzas_Vittoria.Infrastructure.Repositories.Importacion;

/// <summary>
/// Implementacion de <see cref="IImportRepository"/> usando Dapper + ADO.NET TVPs.
///
/// Responsabilidad unica: armar el <see cref="DataTable"/> con el TVP, configurar
/// los parametros y ejecutar el SP. NO conoce logica de negocio ni de errores de
/// importacion; eso es responsabilidad del SP y del <c>ImportService</c>.
///
/// Esta clase es thread-safe (no tiene estado mutable).
/// </summary>
public class ImportRepository : IImportRepository
{
    public async Task<int> ImportAsync<TDto>(
        string spName,
        string tvpTypeName,
        IEnumerable<TDto> dtos,
        IDbConnection connection,
        IDbTransaction? transaction,
        object? extraParameters = null,
        CancellationToken ct = default) where TDto : class
    {
        if (string.IsNullOrWhiteSpace(spName))
            throw new ArgumentException("El nombre del SP es requerido.", nameof(spName));
        if (string.IsNullOrWhiteSpace(tvpTypeName))
            throw new ArgumentException("El nombre del TVP es requerido.", nameof(tvpTypeName));
        if (connection is null)
            throw new ArgumentNullException(nameof(connection));
        ArgumentNullException.ThrowIfNull(dtos);

        var dataTable = TvpMapper.ToDataTable(dtos);

        // DynamicParameters nos permite controlar el tipo exacto del parametro @Filas
        // (SqlDbType.Object -> Table-Valued Parameter) y mergear los parametros extra
        // del caller sin colisionar con @Filas.
        var parameters = new DynamicParameters();
        parameters.Add(
            name: "@Filas",
            value: dataTable.AsTableValuedParameter(tvpTypeName),
            dbType: DbType.Object,
            direction: ParameterDirection.Input);

        if (extraParameters is not null)
        {
            // AddDynamicParams toma cualquier objeto y mapea sus propiedades publicas
            // como parametros del SP. Los nombres deben coincidir con los parametros del SP
            // (con o sin @, Dapper lo tolera).
            parameters.AddDynamicParams(extraParameters);
        }

        // Usamos ExecuteScalarAsync<int> en lugar de ExecuteAsync para capturar
        // el valor del SELECT @RowCount AS FilasInsertadas al final del SP.
        // ExecuteAsync sobre SPs con SET NOCOUNT ON + BEGIN TRAN puede devolver -1
        // aunque el SP haga RETURN, por eso el patron es devolver un result set escalar.
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                commandText: spName,
                parameters: parameters,
                transaction: transaction,
                cancellationToken: ct,
                commandType: CommandType.StoredProcedure));
    }
}
