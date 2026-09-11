using Cobranzas_Vittoria.Seguridad.Authorization;
using Cobranzas_Vittoria.Tests.Integration.Common;

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
                Permisos.Requerimientos.EditarCantidadesAlmacen,
                Permisos.Requerimientos.ProcesarStock,
                Permisos.Requerimientos.VerDepuracionAlmacen
            }));
            Assert.That(permisosCoordinador, Is.EquivalentTo(new[]
            {
                Permisos.Requerimientos.Ver,
                Permisos.Requerimientos.VerDepuracionAlmacen,
                Permisos.Requerimientos.Aprobar,
                Permisos.Requerimientos.Rechazar,
                Permisos.Requerimientos.EnviarCompras
            }));
            Assert.That(permisosComprador, Is.EquivalentTo(new[]
            {
                Permisos.Requerimientos.Ver,
                Permisos.Requerimientos.VerDepuracionAlmacen
            }));
            Assert.That(permisosAdministrador, Is.EquivalentTo(JwtTestTokenFactory.TodosLosPermisosRequerimientos));
        });
    }

    private static async Task<string[]> ObtenerPermisosAsync(string nombreRol)
        => (await DbHelpers.QueryAsync<string>(@"
            SELECT
                p.Codigo
            FROM seguridad.PermisoRol pr
            INNER JOIN seguridad.Permiso p ON p.IdPermiso = pr.IdPermiso
            INNER JOIN seguridad.Rol r ON r.IdRol = pr.IdRol
            WHERE r.Nombre = @nombreRol
            ORDER BY p.Codigo;",
            new { nombreRol })).ToArray();
}
