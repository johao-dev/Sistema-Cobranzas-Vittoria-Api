using Cobranzas_Vittoria.Application.Importacion.Dtos;
using Cobranzas_Vittoria.Application.Importacion.Persistence;
using Cobranzas_Vittoria.Infrastructure.Repositories.Importacion;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cobranzas_Vittoria.Tests.Integration.Importacion;

/// <summary>
/// Pruebas de integracion de los 6 SPs de carga masiva restantes:
/// Especialidad, Material, Proveedor, ProveedorGastoAdministrativo,
/// ProveedorTerreno, CategoriaGasto.
///
/// Para cada modulo se cubren 2 escenarios minimos:
///   - Happy path: N filas validas -> N inserts.
///   - Error 50001 (CAMPO_OBLIGATORIO): una fila con un campo requerido vacio
///     -> SqlException y rollback completo (ninguna fila del prefijo queda).
///
/// <para>
/// <b>Persistencia entre tests:</b> las 7 tablas de maestra estan en
/// <c>TablesToIgnore</c> del Respawn, por lo que los datos seed y los inserts
/// de tests anteriores persisten. Cada test usa prefijos unicos
/// (<c>PrefijoUnico()</c>) y verifica por prefijo, no por conteo total.
/// </para>
/// </summary>
public class ImportRepositoryModulosExtTests : IntegrationTestBase
{
    private readonly IImportRepository _repo = new ImportRepository(NullLogger<ImportRepository>.Instance);

    // =========================================================================
    // ESPECIALIDAD
    // =========================================================================
    private const string SpEspecialidad = "maestra.usp_Especialidad_CargaMasiva";
    private const string TvpEspecialidad = "maestra.TVP_Especialidad";

    [Test]
    public async Task Especialidad_HappyPath_Inserta3Filas()
    {
        var prefijo = PrefijoUnico();

        var dtos = new[]
        {
            new EspecialidadImportDto { _Fila = 2, Nombre = $"{prefijo}-A", Descripcion = "Desc A", Activo = true },
            new EspecialidadImportDto { _Fila = 3, Nombre = $"{prefijo}-B", Descripcion = null,      Activo = true },
            new EspecialidadImportDto { _Fila = 4, Nombre = $"{prefijo}-C", Descripcion = "Desc C", Activo = false }
        };

        using var connection = AbrirConexion();
        var count = await _repo.ImportAsync(SpEspecialidad, TvpEspecialidad, dtos, connection, transaction: null, extraParameters: new { Usuario = "test-user" });

        Assert.That(count, Is.EqualTo(3));
        Assert.That(ContarEspecialidadPorPrefijo(connection, prefijo), Is.EqualTo(3));
    }

    [Test]
    public async Task Especialidad_NombreVacio_Lanza50001_Rollback()
    {
        var prefijo = PrefijoUnico();

        var dtos = new[]
        {
            new EspecialidadImportDto { _Fila = 2, Nombre = $"{prefijo}-A",   Activo = true },
            new EspecialidadImportDto { _Fila = 3, Nombre = "   ",             Activo = true }, // solo espacios
            new EspecialidadImportDto { _Fila = 4, Nombre = $"{prefijo}-C",   Activo = true }
        };

        using var connection = AbrirConexion();
        var ex = Assert.ThrowsAsync<SqlException>(async () =>
            await _repo.ImportAsync(SpEspecialidad, TvpEspecialidad, dtos, connection, transaction: null, extraParameters: new { Usuario = "test-user" }))!;

        Assert.That(ex.Number, Is.EqualTo(50001));
        Assert.That(ex.Message, Does.Contain("CAMPO_OBLIGATORIO"));
        Assert.That(ContarEspecialidadPorPrefijo(connection, prefijo), Is.EqualTo(0));
    }

    // =========================================================================
    // MATERIAL
    // =========================================================================
    private const string SpMaterial = "maestra.usp_Material_CargaMasiva";
    private const string TvpMaterial = "maestra.TVP_Material";

    [Test]
    public async Task Material_HappyPath_Inserta2Filas_ConYSinCodigo()
    {
        var prefijo = PrefijoUnico();

        var dtos = new[]
        {
            new MaterialImportDto
            {
                _Fila = 2,
                IdEspecialidad = 1,                       // 'prueba' del seed
                Codigo = $"{prefijo}-001",                // codigo manual
                Descripcion = $"{prefijo} desc 1",
                UnidadMedida = "BOL",
                StockMinimo = 10m,
                Activo = true,
                IdUnidadMedida = null,
                CodigoProveedor = "PROV-1"
            },
            new MaterialImportDto
            {
                _Fila = 3,
                IdEspecialidad = 1,
                Codigo = null,                            // SP debe autogenerar MAT-####
                Descripcion = $"{prefijo} desc 2",
                UnidadMedida = "UND",
                StockMinimo = 0m,
                Activo = true,
                IdUnidadMedida = null,
                CodigoProveedor = null
            }
        };

        using var connection = AbrirConexion();
        var count = await _repo.ImportAsync(SpMaterial, TvpMaterial, dtos, connection, transaction: null, extraParameters: new { Usuario = "test-user" });

        Assert.That(count, Is.EqualTo(2));
        Assert.That(ContarMaterialPorPrefijo(connection, prefijo), Is.EqualTo(2));

        // El segundo dto no tenia codigo: verificamos que se genero uno.
        var codigoGenerado = connection.QueryFirstOrDefault<string?>(
            "SELECT Codigo FROM maestra.Material WHERE Descripcion = @Desc",
            new { Desc = $"{prefijo} desc 2" });
        Assert.That(codigoGenerado, Does.StartWith("MAT-"));
    }

    [Test]
    public async Task Material_DescripcionVacia_Lanza50001_Rollback()
    {
        var prefijo = PrefijoUnico();

        var dtos = new[]
        {
            new MaterialImportDto { _Fila = 2, IdEspecialidad = 1, Descripcion = $"{prefijo} ok",  UnidadMedida = "BOL", Activo = true },
            new MaterialImportDto { _Fila = 3, IdEspecialidad = 1, Descripcion = "",                UnidadMedida = "BOL", Activo = true },
            new MaterialImportDto { _Fila = 4, IdEspecialidad = 1, Descripcion = $"{prefijo} ok2", UnidadMedida = "BOL", Activo = true }
        };

        using var connection = AbrirConexion();
        var ex = Assert.ThrowsAsync<SqlException>(async () =>
            await _repo.ImportAsync(SpMaterial, TvpMaterial, dtos, connection, transaction: null, extraParameters: new { Usuario = "test-user" }))!;

        Assert.That(ex.Number, Is.EqualTo(50001));
        Assert.That(ContarMaterialPorPrefijo(connection, prefijo), Is.EqualTo(0));
    }

    [Test]
    public async Task Material_IdEspecialidadInexistente_Lanza50004_FKNoExiste()
    {
        var prefijo = PrefijoUnico();

        var dtos = new[]
        {
            new MaterialImportDto
            {
                _Fila = 2,
                IdEspecialidad = 999_999,             // no existe
                Descripcion = $"{prefijo} test",
                UnidadMedida = "UND",
                Activo = true
            }
        };

        using var connection = AbrirConexion();
        var ex = Assert.ThrowsAsync<SqlException>(async () =>
            await _repo.ImportAsync(SpMaterial, TvpMaterial, dtos, connection, transaction: null, extraParameters: new { Usuario = "test-user" }))!;

        Assert.That(ex.Number, Is.EqualTo(50004));
        Assert.That(ex.Message, Does.Contain("FK_NO_EXISTE"));
    }

    // =========================================================================
    // PROVEEDOR
    // =========================================================================
    private const string SpProveedor = "maestra.usp_Proveedor_CargaMasiva";
    private const string TvpProveedor = "maestra.TVP_Proveedor";

    [Test]
    public async Task Proveedor_HappyPath_Inserta2Filas()
    {
        var prefijo = PrefijoUnico();
        // Rucs derivados del prefijo (max 20 chars). Usamos 11 digitos en formato
        // "20XXXXXXXXX" (prefijo de 12 chars partido en dos mitades de 9 digitos).
        var ruc1 = $"20{prefijo.Substring(0, 9)}01";
        var ruc2 = $"20{prefijo.Substring(3, 9)}02";

        var dtos = new[]
        {
            new ProveedorImportDto
            {
                _Fila = 2,
                RazonSocial = $"{prefijo} SAC",
                Ruc = ruc1,
                Contacto = "Contacto 1",
                Telefono = "999888777",
                Correo = "a@b.com",
                Banco = "BCP",
                CuentaCorriente = "111",
                TrabajamosConProveedor = "SI",
                Activo = true
            },
            new ProveedorImportDto
            {
                _Fila = 3,
                RazonSocial = $"{prefijo} EIRL",
                Ruc = ruc2,
                Contacto = null,
                Telefono = null,
                Activo = true
            }
        };

        using var connection = AbrirConexion();
        var count = await _repo.ImportAsync(SpProveedor, TvpProveedor, dtos, connection, transaction: null, extraParameters: new { Usuario = "test-user" });

        Assert.That(count, Is.EqualTo(2));
        Assert.That(ContarProveedorPorPrefijo(connection, prefijo), Is.EqualTo(2));
    }

    [Test]
    public async Task Proveedor_RucVacio_Lanza50001_Rollback()
    {
        var prefijo = PrefijoUnico();

        var dtos = new[]
        {
            new ProveedorImportDto { _Fila = 2, RazonSocial = $"{prefijo} A", Ruc = "11111111111", Activo = true },
            new ProveedorImportDto { _Fila = 3, RazonSocial = $"{prefijo} B", Ruc = "",            Activo = true }
        };

        using var connection = AbrirConexion();
        var ex = Assert.ThrowsAsync<SqlException>(async () =>
            await _repo.ImportAsync(SpProveedor, TvpProveedor, dtos, connection, transaction: null, extraParameters: new { Usuario = "test-user" }))!;

        Assert.That(ex.Number, Is.EqualTo(50001));
        Assert.That(ContarProveedorPorPrefijo(connection, prefijo), Is.EqualTo(0));
    }

    [Test]
    public async Task Proveedor_RucYaExisteEnBD_Lanza50003()
    {
        // '20601997291' es el Ruc de 'ACG EDIFICACIONES EIRL' del seed.
        var prefijo = PrefijoUnico();

        var dtos = new[]
        {
            new ProveedorImportDto
            {
                _Fila = 2,
                RazonSocial = $"{prefijo} nueva",
                Ruc = "20601997291",
                Activo = true
            }
        };

        using var connection = AbrirConexion();
        var ex = Assert.ThrowsAsync<SqlException>(async () =>
            await _repo.ImportAsync(SpProveedor, TvpProveedor, dtos, connection, transaction: null, extraParameters: new { Usuario = "test-user" }))!;

        Assert.That(ex.Number, Is.EqualTo(50003));
        Assert.That(ex.Message, Does.Contain("VALOR_YA_EXISTE_EN_BD"));
    }

    // =========================================================================
    // PROVEEDOR GASTO ADMINISTRATIVO
    // =========================================================================
    private const string SpProveedorGasto = "maestra.usp_ProveedorGastoAdministrativo_CargaMasiva";
    private const string TvpProveedorGasto = "maestra.TVP_ProveedorGastoAdministrativo";

    [Test]
    public async Task ProveedorGasto_HappyPath_Inserta2Filas()
    {
        var prefijo = PrefijoUnico();
        var ruc1 = $"20{prefijo.Substring(0, 9)}01";

        var dtos = new[]
        {
            new ProveedorGastoAdministrativoImportDto
            {
                _Fila = 2, RazonSocial = $"{prefijo} A", Ruc = ruc1,
                Contacto = "C1", Correo = "a@b.com", Activo = true, IdCategoriaGasto = null
            },
            new ProveedorGastoAdministrativoImportDto
            {
                _Fila = 3, RazonSocial = $"{prefijo} B", Ruc = null,
                Contacto = null, Activo = true, IdCategoriaGasto = 1   // CategoriaGasto del seed
            }
        };

        using var connection = AbrirConexion();
        var count = await _repo.ImportAsync(SpProveedorGasto, TvpProveedorGasto, dtos, connection, transaction: null, extraParameters: new { Usuario = "test-user" });

        Assert.That(count, Is.EqualTo(2));
        Assert.That(ContarProveedorGastoPorPrefijo(connection, prefijo), Is.EqualTo(2));
    }

    [Test]
    public async Task ProveedorGasto_RazonSocialVacia_Lanza50001_Rollback()
    {
        var prefijo = PrefijoUnico();

        var dtos = new[]
        {
            new ProveedorGastoAdministrativoImportDto { _Fila = 2, RazonSocial = $"{prefijo} A", Activo = true },
            new ProveedorGastoAdministrativoImportDto { _Fila = 3, RazonSocial = "  ",          Activo = true }
        };

        using var connection = AbrirConexion();
        var ex = Assert.ThrowsAsync<SqlException>(async () =>
            await _repo.ImportAsync(SpProveedorGasto, TvpProveedorGasto, dtos, connection, transaction: null, extraParameters: new { Usuario = "test-user" }))!;

        Assert.That(ex.Number, Is.EqualTo(50001));
        Assert.That(ContarProveedorGastoPorPrefijo(connection, prefijo), Is.EqualTo(0));
    }

    // =========================================================================
    // PROVEEDOR TERRENO
    // =========================================================================
    private const string SpProveedorTerreno = "maestra.usp_ProveedorTerreno_CargaMasiva";
    private const string TvpProveedorTerreno = "maestra.TVP_ProveedorTerreno";

    [Test]
    public async Task ProveedorTerreno_HappyPath_Inserta2Filas()
    {
        var prefijo = PrefijoUnico();

        var dtos = new[]
        {
            new ProveedorTerrenoImportDto
            {
                _Fila = 2, RazonSocial = $"{prefijo} A", Ruc = "20123456789", Contacto = "C1", Telefono = "999111", Activo = true
            },
            new ProveedorTerrenoImportDto
            {
                _Fila = 3, RazonSocial = $"{prefijo} B", Ruc = null, Activo = true
            }
        };

        using var connection = AbrirConexion();
        var count = await _repo.ImportAsync(SpProveedorTerreno, TvpProveedorTerreno, dtos, connection, transaction: null, extraParameters: new { Usuario = "test-user" });

        Assert.That(count, Is.EqualTo(2));
        Assert.That(ContarProveedorTerrenoPorPrefijo(connection, prefijo), Is.EqualTo(2));
    }

    [Test]
    public async Task ProveedorTerreno_RazonSocialVacia_Lanza50001_Rollback()
    {
        var prefijo = PrefijoUnico();

        var dtos = new[]
        {
            new ProveedorTerrenoImportDto { _Fila = 2, RazonSocial = $"{prefijo} A", Activo = true },
            new ProveedorTerrenoImportDto { _Fila = 3, RazonSocial = "",            Activo = true }
        };

        using var connection = AbrirConexion();
        var ex = Assert.ThrowsAsync<SqlException>(async () =>
            await _repo.ImportAsync(SpProveedorTerreno, TvpProveedorTerreno, dtos, connection, transaction: null, extraParameters: new { Usuario = "test-user" }))!;

        Assert.That(ex.Number, Is.EqualTo(50001));
        Assert.That(ContarProveedorTerrenoPorPrefijo(connection, prefijo), Is.EqualTo(0));
    }

    // =========================================================================
    // CATEGORIA GASTO
    // =========================================================================
    private const string SpCategoriaGasto = "maestra.usp_CategoriaGasto_CargaMasiva";
    private const string TvpCategoriaGasto = "maestra.TVP_CategoriaGasto";

    [Test]
    public async Task CategoriaGasto_HappyPath_Inserta2Filas()
    {
        var prefijo = PrefijoUnico();

        var dtos = new[]
        {
            new CategoriaGastoImportDto { _Fila = 2, Nombre = $"{prefijo}-A", Activo = true },
            new CategoriaGastoImportDto { _Fila = 3, Nombre = $"{prefijo}-B", Activo = true }
        };

        using var connection = AbrirConexion();
        var count = await _repo.ImportAsync(SpCategoriaGasto, TvpCategoriaGasto, dtos, connection, transaction: null, extraParameters: new { Usuario = "test-user" });

        Assert.That(count, Is.EqualTo(2));
        Assert.That(ContarCategoriaGastoPorPrefijo(connection, prefijo), Is.EqualTo(2));
    }

    [Test]
    public async Task CategoriaGasto_NombreVacio_Lanza50001_Rollback()
    {
        var prefijo = PrefijoUnico();

        var dtos = new[]
        {
            new CategoriaGastoImportDto { _Fila = 2, Nombre = $"{prefijo}-A", Activo = true },
            new CategoriaGastoImportDto { _Fila = 3, Nombre = "",             Activo = true }
        };

        using var connection = AbrirConexion();
        var ex = Assert.ThrowsAsync<SqlException>(async () =>
            await _repo.ImportAsync(SpCategoriaGasto, TvpCategoriaGasto, dtos, connection, transaction: null, extraParameters: new { Usuario = "test-user" }))!;

        Assert.That(ex.Number, Is.EqualTo(50001));
        Assert.That(ContarCategoriaGastoPorPrefijo(connection, prefijo), Is.EqualTo(0));
    }

    // =========================================================================
    // Helpers
    // =========================================================================
    private static SqlConnection AbrirConexion()
    {
        var connection = new SqlConnection(GlobalSetupFixture.DbContainer.GetConnectionString());
        connection.Open();
        return connection;
    }

    private static int ContarEspecialidadPorPrefijo(System.Data.IDbConnection connection, string prefijo)
        => connection.QueryFirstOrDefault<int>(
            "SELECT COUNT(*) FROM maestra.Especialidad WHERE Nombre LIKE @Patron + '%'",
            new { Patron = prefijo });

    private static int ContarMaterialPorPrefijo(System.Data.IDbConnection connection, string prefijo)
        => connection.QueryFirstOrDefault<int>(
            "SELECT COUNT(*) FROM maestra.Material WHERE Descripcion LIKE @Patron + '%'",
            new { Patron = prefijo });

    private static int ContarProveedorPorPrefijo(System.Data.IDbConnection connection, string prefijo)
        => connection.QueryFirstOrDefault<int>(
            "SELECT COUNT(*) FROM maestra.Proveedor WHERE RazonSocial LIKE @Patron + '%'",
            new { Patron = prefijo });

    private static int ContarProveedorGastoPorPrefijo(System.Data.IDbConnection connection, string prefijo)
        => connection.QueryFirstOrDefault<int>(
            "SELECT COUNT(*) FROM maestra.ProveedorGastoAdministrativo WHERE RazonSocial LIKE @Patron + '%'",
            new { Patron = prefijo });

    private static int ContarProveedorTerrenoPorPrefijo(System.Data.IDbConnection connection, string prefijo)
        => connection.QueryFirstOrDefault<int>(
            "SELECT COUNT(*) FROM maestra.ProveedorTerreno WHERE RazonSocial LIKE @Patron + '%'",
            new { Patron = prefijo });

    private static int ContarCategoriaGastoPorPrefijo(System.Data.IDbConnection connection, string prefijo)
        => connection.QueryFirstOrDefault<int>(
            "SELECT COUNT(*) FROM maestra.CategoriaGasto WHERE Nombre LIKE @Patron + '%'",
            new { Patron = prefijo });

    private static string PrefijoUnico() => $"T{Guid.NewGuid():N}".Substring(0, 12);
}
