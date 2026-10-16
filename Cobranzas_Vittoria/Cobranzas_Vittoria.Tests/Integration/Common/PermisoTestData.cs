using Dapper;
using Microsoft.Data.SqlClient;

namespace Cobranzas_Vittoria.Tests.Integration.Common;

public static class PermisoTestData
{
    // El catalogo se mantiene por migraciones; los escenarios de prueba se preparan sin altas por API.
    public static async Task<int> InsertarAsync(
        string codigo,
        string nombre,
        string descripcion = "",
        bool activo = true)
    {
        await using SqlConnection connection = await DbHelpers.OpenTestConnectionAsync();

        return await connection.QuerySingleAsync<int>("""
            INSERT INTO seguridad.Permiso
                (Codigo, Nombre, Descripcion, Activo, FechaCreacion, UsuarioCreacion)
            OUTPUT INSERTED.IdPermiso
            VALUES (@codigo, @nombre, @descripcion, @activo, SYSUTCDATETIME(), 'test');
            """, new { codigo, nombre, descripcion, activo });
    }
}
