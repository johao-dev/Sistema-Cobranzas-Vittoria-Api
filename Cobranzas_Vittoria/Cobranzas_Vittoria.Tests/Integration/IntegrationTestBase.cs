using Cobranzas_Vittoria.Tests.Setup;
using Microsoft.Data.SqlClient;
using Respawn;
using Respawn.Graph;

namespace Cobranzas_Vittoria.Tests.Integration;

public abstract class IntegrationTestBase
{
    protected HttpClient _client => GlobalSetupFixture.Client;
    private static Respawner? _respawner;
    private static readonly object _initLock = new();

    [OneTimeSetUp]
    public async Task BaseOneTimeSetup()
    {
        if (_respawner != null) return;

        await using var connection = new SqlConnection(GlobalSetupFixture.DbContainer.GetConnectionString());
        await connection.OpenAsync();

        lock (_initLock)
        {
            _respawner ??= Respawner.CreateAsync(connection,
            new RespawnerOptions
            {
                DbAdapter = DbAdapter.SqlServer,
                TablesToIgnore =
                [
                    // DbUp History
                    new Table("dbo", "SchemaVersions"),

                    // seguridad
                    new Table("seguridad", "Rol"),
                    new Table("seguridad", "Usuario"),
                    new Table("seguridad", "UsuarioRol"),

                    // maestra
                    new Table("maestra", "Especialidad"),
                    new Table("maestra", "UnidadMedida"),
                    new Table("maestra", "CategoriaGasto"),
                    new Table("maestra", "Proveedor"),
                    new Table("maestra", "ProveedorTerreno"),
                    new Table("maestra", "ProveedorGastoAdministrativo"),
                    new Table("maestra", "ProveedorEspecialidadCotizacion"),
                    new Table("maestra", "ProveedorEspecialidad"),
                    new Table("maestra", "ProveedorReglaValorizacion"),
                    new Table("maestra", "Material"),
                    new Table("maestra", "Proyecto"),

                    // contable (datos seed)
                    new Table("contable", "CotizacionMaterialEspecialidad"),
                ]
            }).GetAwaiter().GetResult();
        }
    }

    [SetUp]
    public async Task ResetDatabaseBeforeEachTest()
    {
        await using var connection = new SqlConnection(GlobalSetupFixture.DbContainer.GetConnectionString());
        await connection.OpenAsync();
        await _respawner!.ResetAsync(connection);
    }
}
