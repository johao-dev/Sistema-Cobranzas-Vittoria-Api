using Microsoft.Data.SqlClient;

namespace Cobranzas_Vittoria.Tests.Integration.Common;

/// <summary>
/// Helpers para verificar efectos en BD tras operaciones HTTP.
/// Usa la cadena de conexión del contenedor de Testcontainers,
/// nunca el connection string de la app de producción.
/// </summary>
public static class DbHelpers
{
    /// <summary>
    /// Obtiene una conexión ABIERTA al SQL Server de pruebas.
    /// Equivalente a lo que IDbConnectionFactory.CreateConnection() hace,
    /// pero accesible desde los tests sin tener que resolver el servicio del DI.
    /// </summary>
    public static async Task<SqlConnection> OpenTestConnectionAsync()
    {
        var conn = new SqlConnection(GlobalSetupFixture.DbContainer.GetConnectionString());
        await conn.OpenAsync();
        return conn;
    }

    /// <summary>
    /// Ejecuta un SELECT escalar y devuelve el primer valor de la primera fila.
    /// Útil para asserts como "existe el proyecto con nombre X".
    /// </summary>
    public static async Task<T?> QueryScalarAsync<T>(string sql, object? parameters = null)
    {
        await using var conn = await OpenTestConnectionAsync();
        return await Dapper.SqlMapper.QueryFirstOrDefaultAsync<T?>(conn, sql, parameters);
    }

    /// <summary>
    /// Ejecuta un SELECT que devuelve un set de filas (proyectado a T).
    /// </summary>
    public static async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? parameters = null)
    {
        await using var conn = await OpenTestConnectionAsync();
        return await Dapper.SqlMapper.QueryAsync<T>(conn, sql, parameters);
    }
}
