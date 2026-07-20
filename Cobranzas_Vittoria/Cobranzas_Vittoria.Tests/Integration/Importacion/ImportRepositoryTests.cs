using Cobranzas_Vittoria.Application.Importacion.Dtos;
using Cobranzas_Vittoria.Application.Importacion.Persistence;
using Cobranzas_Vittoria.Infrastructure.Repositories.Importacion;
using Cobranzas_Vittoria.Tests.Setup;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cobranzas_Vittoria.Tests.Integration.Importacion;

/// <summary>
/// Pruebas de integracion del <see cref="ImportRepository"/> con SQL Server real (Testcontainers).
///
/// Cubre los flujos del piloto <c>UnidadMedida</c>:
///   - Happy path: 5 filas validas -&gt; 5 inserts
///   - Error por obligatoriedad (Codigo vacio) -&gt; 50001, rollback completo
///   - Error por duplicado intra-archivo -&gt; 50002, rollback completo
///   - Error por Codigo ya existente en BD -&gt; 50003, rollback completo
///   - FechaCreacion se setea via GETDATE() en el SP
///   - @Usuario se acepta aunque la tabla no tenga la columna (parametro declarado pero no usado)
///
/// <para>
/// <b>Importante:</b> la tabla <c>maestra.UnidadMedida</c> esta en
/// <c>TablesToIgnore</c> del Respawn, por lo que los datos seed (UM-001, BAL, BOL, etc.)
/// y los inserts de tests anteriores persisten. Cada test usa Codigos con prefijo
/// unico (TEST-IMPORT-{guid}-XXX) y verifica por prefijo, no por conteo total.
/// </para>
/// </summary>
public class ImportRepositoryTests : IntegrationTestBase
{
    private const string SpName = "maestra.usp_UnidadMedida_CargaMasiva";
    private const string TvpTypeName = "maestra.TVP_UnidadMedida";

    private readonly IImportRepository _repo = new ImportRepository(NullLogger<ImportRepository>.Instance);

    [Test]
    public async Task Import_5FilasValidas_Inserta5Filas()
    {
        var prefijo = PrefijoUnico();
        var dtos = CrearDtosValidos(prefijo, cantidad: 5);

        using var connection = AbrirConexion();
        var count = await _repo.ImportAsync(SpName, TvpTypeName, dtos, connection, transaction: null, extraParameters: new { Usuario = "test-user" });

        Assert.That(count, Is.EqualTo(5));

        var insertados = ContarPorPrefijo(connection, prefijo);
        Assert.That(insertados, Is.EqualTo(5));
    }

    [Test]
    public async Task Import_FilaConCodigoVacio_LanzaSqlException_NoInsertaNinguna()
    {
        var prefijo = PrefijoUnico();
        var dtos = new[]
        {
            new UnidadMedidaImportDto { _Fila = 2, Codigo = $"{prefijo}-001", Nombre = "Valido 1", Activo = true },
            new UnidadMedidaImportDto { _Fila = 3, Codigo = "",               Nombre = "Codigo Vacio", Activo = true },
            new UnidadMedidaImportDto { _Fila = 4, Codigo = $"{prefijo}-003", Nombre = "Valido 3", Activo = true }
        };

        using var connection = AbrirConexion();
        var ex = Assert.ThrowsAsync<SqlException>(async () =>
            await _repo.ImportAsync(SpName, TvpTypeName, dtos, connection, transaction: null, extraParameters: new { Usuario = "test-user" }))!;

        Assert.That(ex.Number, Is.EqualTo(50001));
        Assert.That(ex.Message, Does.Contain("CODIGO_NOMBRE_OBLIGATORIO"));

        // Rollback: ninguna fila del prefijo debe quedar
        var insertados = ContarPorPrefijo(connection, prefijo);
        Assert.That(insertados, Is.EqualTo(0));
    }

    [Test]
    public async Task Import_FilaConNombreVacio_LanzaSqlException_NoInsertaNinguna()
    {
        var prefijo = PrefijoUnico();
        var dtos = new[]
        {
            new UnidadMedidaImportDto { _Fila = 2, Codigo = $"{prefijo}-001", Nombre = "",               Activo = true },
            new UnidadMedidaImportDto { _Fila = 3, Codigo = $"{prefijo}-002", Nombre = "Valido 2",     Activo = true }
        };

        using var connection = AbrirConexion();
        var ex = Assert.ThrowsAsync<SqlException>(async () =>
            await _repo.ImportAsync(SpName, TvpTypeName, dtos, connection, transaction: null, extraParameters: new { Usuario = "test-user" }))!;

        Assert.That(ex.Number, Is.EqualTo(50001));

        var insertados = ContarPorPrefijo(connection, prefijo);
        Assert.That(insertados, Is.EqualTo(0));
    }

    [Test]
    public async Task Import_CodigosDuplicadosEnArchivo_LanzaSqlException_NoInsertaNinguna()
    {
        var prefijo = PrefijoUnico();
        var codigoDuplicado = $"{prefijo}-DUP";
        var dtos = new[]
        {
            new UnidadMedidaImportDto { _Fila = 2, Codigo = codigoDuplicado, Nombre = "Primera",   Activo = true },
            new UnidadMedidaImportDto { _Fila = 3, Codigo = $"{prefijo}-002", Nombre = "Unico",     Activo = true },
            new UnidadMedidaImportDto { _Fila = 4, Codigo = codigoDuplicado, Nombre = "Duplicado", Activo = true }
        };

        using var connection = AbrirConexion();
        var ex = Assert.ThrowsAsync<SqlException>(async () =>
            await _repo.ImportAsync(SpName, TvpTypeName, dtos, connection, transaction: null, extraParameters: new { Usuario = "test-user" }))!;

        Assert.That(ex.Number, Is.EqualTo(50002));
        Assert.That(ex.Message, Does.Contain("CODIGO_DUPLICADO_EN_ARCHIVO"));

        var insertados = ContarPorPrefijo(connection, prefijo);
        Assert.That(insertados, Is.EqualTo(0));
    }

    [Test]
    public async Task Import_CodigoYaExisteEnBD_LanzaSqlException_NoInsertaNinguna()
    {
        // UM-001 es parte del seed data (V1_1_0__SeedData.sql) y persiste porque
        // maestra.UnidadMedida esta en TablesToIgnore del Respawn.
        var prefijo = PrefijoUnico();
        var dtos = new[]
        {
            new UnidadMedidaImportDto { _Fila = 2, Codigo = $"{prefijo}-001", Nombre = "Nueva 1", Activo = true },
            new UnidadMedidaImportDto { _Fila = 3, Codigo = "UM-001",         Nombre = "Existente", Activo = true }
        };

        using var connection = AbrirConexion();
        var ex = Assert.ThrowsAsync<SqlException>(async () =>
            await _repo.ImportAsync(SpName, TvpTypeName, dtos, connection, transaction: null, extraParameters: new { Usuario = "test-user" }))!;

        Assert.That(ex.Number, Is.EqualTo(50003));
        Assert.That(ex.Message, Does.Contain("CODIGO_YA_EXISTE_EN_BD"));

        // Ninguna fila del prefijo nuevo quedo insertada
        var insertados = ContarPorPrefijo(connection, prefijo);
        Assert.That(insertados, Is.EqualTo(0));
    }

    [Test]
    public async Task Import_FechaCreacionSeSeteaConGetDate_DelSP()
    {
        var prefijo = PrefijoUnico();
        var dtos = CrearDtosValidos(prefijo, cantidad: 1);
        // GETDATE() devuelve la hora del servidor SQL en UTC. Comparamos contra
        // DateTime.UtcNow para evitar falsos negativos por diferencia de zona horaria
        // (el cliente de tests corre en America/Lima = UTC-5).
        var antesDeInsert = DateTime.UtcNow.AddSeconds(-2);

        using var connection = AbrirConexion();
        await _repo.ImportAsync(SpName, TvpTypeName, dtos, connection, transaction: null, extraParameters: new { Usuario = "test-user" });

        var fechaCreacion = connection.QueryFirstOrDefault<DateTime>(
            $"SELECT FechaCreacion FROM maestra.UnidadMedida WHERE Codigo = @Codigo",
            new { Codigo = $"{prefijo}-001" });

        Assert.That(fechaCreacion, Is.GreaterThan(antesDeInsert));
        Assert.That(fechaCreacion, Is.LessThan(DateTime.UtcNow.AddSeconds(2)));
    }

    [Test]
    public async Task Import_ListaVacia_NoInsertaNinguna()
    {
        using var connection = AbrirConexion();
        var count = await _repo.ImportAsync(SpName, TvpTypeName, Array.Empty<UnidadMedidaImportDto>(), connection, transaction: null, extraParameters: new { Usuario = "test-user" });
        Assert.That(count, Is.EqualTo(0));
    }

    [Test]
    public async Task Import_ExtraParametroUsuario_NoFallaAunqueLaTablaNoLoUse()
    {
        // maestra.UnidadMedida NO tiene columna UsuarioCreacion, pero el SP declara
        // @Usuario (default NULL) para mantener uniforme el contrato con el repository.
        // Este test verifica que pasar @Usuario no rompe la ejecucion.
        var prefijo = PrefijoUnico();
        var dtos = CrearDtosValidos(prefijo, cantidad: 1);

        using var connection = AbrirConexion();
        Assert.DoesNotThrowAsync(async () =>
            await _repo.ImportAsync(SpName, TvpTypeName, dtos, connection, transaction: null, extraParameters: new { Usuario = "usuario-de-prueba" }));

        var insertados = ContarPorPrefijo(connection, prefijo);
        Assert.That(insertados, Is.EqualTo(1));
    }

    // --- Helpers ---

    private static SqlConnection AbrirConexion()
    {
        var connection = new SqlConnection(GlobalSetupFixture.DbContainer.GetConnectionString());
        connection.Open();
        return connection;
    }

    private static UnidadMedidaImportDto[] CrearDtosValidos(string prefijo, int cantidad)
    {
        return Enumerable.Range(1, cantidad)
            .Select(i => new UnidadMedidaImportDto
            {
                _Fila = i + 1,
                Codigo = $"{prefijo}-{i:000}",
                Nombre = $"Unidad {prefijo}-{i:000}",
                Activo = true
            })
            .ToArray();
    }

    private static int ContarPorPrefijo(System.Data.IDbConnection connection, string prefijo)
    {
        return connection.QueryFirstOrDefault<int>(
            "SELECT COUNT(*) FROM maestra.UnidadMedida WHERE Codigo LIKE @Patron + '%'",
            new { Patron = prefijo });
    }

    private static string PrefijoUnico() => $"T{Guid.NewGuid():N}".Substring(0, 12);
}
