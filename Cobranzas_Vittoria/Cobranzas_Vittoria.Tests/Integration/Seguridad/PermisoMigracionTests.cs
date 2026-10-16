using Cobranzas_Vittoria.Tests.Integration.Common;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Cobranzas_Vittoria.Tests.Integration.Seguridad;

public class PermisoMigracionTests : IntegrationTestBase
{
    [Test]
    public async Task RetiroSps_EliminaProcedimientosExistentesYPermiteReejecucion()
    {
        using StreamReader reader = new StreamReader(typeof(Program).Assembly.GetManifestResourceStream(
            "Cobranzas_Vittoria.Migrations.Versioned.V2_7_7__Seguridad_Retira_Sps_Alta_Baja_Permiso.sql")!);
        string script = await reader.ReadToEndAsync();
        string[] procedimientosRetirados =
        [
            "usp_Permiso_GetByCodigo",
            "usp_Permiso_Insert",
            "usp_Permiso_Delete"
        ];

        await using SqlConnection connection = await DbHelpers.OpenTestConnectionAsync();
        using SqlTransaction transaction = connection.BeginTransaction();

        // El rollback mantiene los objetos de la base compartida como estaban antes del test.
        try
        {
            foreach (string procedimiento in procedimientosRetirados)
            {
                await connection.ExecuteAsync(
                    $"CREATE OR ALTER PROCEDURE seguridad.{procedimiento} AS SELECT 1;",
                    transaction: transaction);
            }

            await connection.ExecuteAsync(script, transaction: transaction);
            await connection.ExecuteAsync(script, transaction: transaction);

            int cantidadRetirados = await connection.QuerySingleAsync<int>("""
                SELECT COUNT(*) FROM sys.procedures
                WHERE schema_id = SCHEMA_ID('seguridad') AND name IN @procedimientosRetirados;
                """, new { procedimientosRetirados }, transaction);
            int cantidadVigentes = await connection.QuerySingleAsync<int>("""
                SELECT COUNT(*) FROM sys.procedures
                WHERE schema_id = SCHEMA_ID('seguridad')
                  AND name IN ('usp_Permiso_GetById', 'usp_Permiso_List', 'usp_Permiso_Update');
                """, transaction: transaction);

            Assert.Multiple(() =>
            {
                Assert.That(cantidadRetirados, Is.Zero);
                Assert.That(cantidadVigentes, Is.EqualTo(3));
            });
        }
        finally
        {
            transaction.Rollback();
        }
    }
}
