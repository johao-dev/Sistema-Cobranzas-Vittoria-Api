using Cobranzas_Vittoria.Seguridad.Authorization;
using Cobranzas_Vittoria.Tests.Integration.Common;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Cobranzas_Vittoria.Tests.Integration.Seguridad;

[TestFixture]
public sealed class RbacSeedTests : IntegrationTestBase
{
    [Test]
    public async Task MatrizInicial_CreaRolesYAsociacionesDeRequerimientos()
    {
        string[] roles = (await DbHelpers.QueryAsync<string>(
            "SELECT Nombre FROM seguridad.Rol WHERE Nombre IN @nombres",
            new { nombres = new[] { "Administrador", "Almacenero", "Residente", "Coordinador", "Comprador" } }))
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(roles, Does.Contain("Administrador"));
            Assert.That(roles, Does.Contain("Almacenero"));
            Assert.That(roles, Does.Contain("Residente"));
            Assert.That(roles, Does.Contain("Coordinador"));
            Assert.That(roles, Does.Contain("Comprador"));
            Assert.That(roles, Does.Not.Contain("almacen"));
        });

        string[] permisosResidente = await ObtenerPermisosAsync("Residente");
        string[] permisosAlmacenero = await ObtenerPermisosAsync("Almacenero");
        string[] permisosCoordinador = await ObtenerPermisosAsync("Coordinador");
        string[] permisosComprador = await ObtenerPermisosAsync("Comprador");
        string[] permisosAdministrador = await ObtenerPermisosAsync("Administrador");

        Assert.Multiple(() =>
        {
            Assert.That(permisosResidente, Is.EquivalentTo(new[]
            {
                Permisos.Requerimientos.Ver,
                Permisos.Requerimientos.Crear,
                Permisos.Requerimientos.EditarBorrador,
                Permisos.Requerimientos.Enviar
            }));
            Assert.That(permisosAlmacenero, Is.EquivalentTo(new[]
            {
                Permisos.Requerimientos.Ver,
                Permisos.Requerimientos.GestionarItemsAlmacen,
                Permisos.Requerimientos.ProcesarStock,
            }));
            Assert.That(permisosCoordinador, Is.EquivalentTo(new[]
            {
                Permisos.Requerimientos.Ver,
                Permisos.Requerimientos.Aprobar,
                Permisos.Requerimientos.Rechazar,
                Permisos.Requerimientos.EnviarCompras
            }));
            Assert.That(permisosComprador, Is.EquivalentTo(new[]
            {
                Permisos.Requerimientos.Ver,
            }));
            Assert.That(permisosAdministrador, Is.EquivalentTo(JwtTestTokenFactory.TodosLosPermisosRequerimientos));
        });
    }

    [Test]
    public async Task MatrizActual_NoContieneElPermisoRetirado()
    {
        int cantidad = await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM seguridad.Permiso WHERE Codigo = @codigo",
            new { codigo = "requerimientos.editar_cantidades_almacen" });

        Assert.That(cantidad, Is.Zero);
    }

    [Test]
    public async Task MigracionRetiro_EliminaAsignacionesYEsIdempotenteSinAmpliarPrivilegios()
    {
        using StreamReader reader = new StreamReader(typeof(Program).Assembly.GetManifestResourceStream(
            "Cobranzas_Vittoria.Migrations.Versioned.V2_7_6__Seguridad_Retira_Permiso_Editar_Cantidades_Almacen.sql")!);
        string script = await reader.ReadToEndAsync();

        await using SqlConnection connection = await DbHelpers.OpenTestConnectionAsync();
        using SqlTransaction transaction = connection.BeginTransaction();

        // Seguridad no se limpia con Respawn: el rollback evita alterar la matriz de otros tests.
        try
        {
            int[] rolesAntes = (await connection.QueryAsync<int>(@"
                SELECT pr.IdRol
                FROM seguridad.PermisoRol pr
                INNER JOIN seguridad.Permiso p ON p.IdPermiso = pr.IdPermiso
                WHERE p.Codigo = @codigo;",
                new { codigo = Permisos.Requerimientos.GestionarItemsAlmacen }, transaction)).ToArray();

            int idPermisoRetirado = await connection.QuerySingleAsync<int>(@"
                INSERT INTO seguridad.Permiso
                    (Codigo, Nombre, Descripcion, Activo, FechaCreacion, UsuarioCreacion)
                OUTPUT INSERTED.IdPermiso
                VALUES ('requerimientos.editar_cantidades_almacen', 'Permiso retirado',
                    'Escenario anterior a la migración', 1, SYSUTCDATETIME(), 'integration-test');",
                transaction: transaction);

            await connection.ExecuteAsync(@"
                INSERT INTO seguridad.PermisoRol (IdPermiso, IdRol, FechaCreacion, UsuarioCreacion)
                SELECT @idPermisoRetirado, IdRol, SYSUTCDATETIME(), 'integration-test'
                FROM seguridad.Rol;",
                new { idPermisoRetirado }, transaction);

            await connection.ExecuteAsync(script, transaction: transaction);
            await connection.ExecuteAsync(script, transaction: transaction);

            int permisosRetirados = await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM seguridad.Permiso WHERE IdPermiso = @idPermisoRetirado",
                new { idPermisoRetirado }, transaction);
            int asignacionesRetiradas = await connection.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM seguridad.PermisoRol WHERE IdPermiso = @idPermisoRetirado",
                new { idPermisoRetirado }, transaction);
            int[] rolesDespues = (await connection.QueryAsync<int>(@"
                SELECT pr.IdRol
                FROM seguridad.PermisoRol pr
                INNER JOIN seguridad.Permiso p ON p.IdPermiso = pr.IdPermiso
                WHERE p.Codigo = @codigo;",
                new { codigo = Permisos.Requerimientos.GestionarItemsAlmacen }, transaction)).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(permisosRetirados, Is.Zero);
                Assert.That(asignacionesRetiradas, Is.Zero);
                Assert.That(rolesDespues, Is.EquivalentTo(rolesAntes));
            });
        }
        finally
        {
            transaction.Rollback();
        }
    }

    private static async Task<string[]> ObtenerPermisosAsync(string nombreRol)
        => (await DbHelpers.QueryAsync<string>(@"
            SELECT
                p.Codigo
            FROM seguridad.PermisoRol pr
            INNER JOIN seguridad.Permiso p ON p.IdPermiso = pr.IdPermiso
            INNER JOIN seguridad.Rol r ON r.IdRol = pr.IdRol
            WHERE r.Nombre = @nombreRol
              AND p.Codigo LIKE 'requerimientos.%'
            ORDER BY p.Codigo;",
            new { nombreRol })).ToArray();
}
