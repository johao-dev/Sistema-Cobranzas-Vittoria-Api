using Cobranzas_Vittoria.Application.Importacion.Dtos;
using Cobranzas_Vittoria.Application.Importacion.Parsers;
using Cobranzas_Vittoria.Application.Importacion.Persistence;
using Cobranzas_Vittoria.Application.Importacion.Processors;
using Cobranzas_Vittoria.Application.Importacion.Services;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Domain.Importacion;
using Cobranzas_Vittoria.Tests.Unit.Importacion.Stubs;
using Microsoft.Extensions.Logging.Abstractions;
using System.Data;

namespace Cobranzas_Vittoria.Tests.Unit.Importacion.Processors;

/// <summary>
/// Pruebas unitarias de los 7 processors concretos de importacion masiva.
/// Se enfoca en <c>MapearFila</c>: verificar que el mapeo de columnas del
/// archivo a las propiedades del DTO funciona correctamente y que se reportan
/// errores especificos cuando hay campos requeridos vacios o tipos invalidos.
///
/// No se ejercita la transaccion ni el SP (eso lo cubre la integracion con
/// Testcontainers en Fase 7).
/// </summary>
public class ImportProcessorsUnitTests
{
    private readonly FileParserResolver _parserResolver;
    private readonly IImportRepository _repository = new StubRepository();
    private readonly IDbConnectionFactory _connectionFactory = new StubConnectionFactory();

    public ImportProcessorsUnitTests()
    {
        _parserResolver = new FileParserResolver(new IFileParser[]
        {
            new CsvFileParser(),
            new ExcelFileParser()
        });
    }

    // =========================================================================
    // UnidadMedidaImportProcessor
    // =========================================================================

    [Test]
    public void UnidadMedida_FilaValida_DevuelveDtoConDatosCorrectos()
    {
        var processor = new UnidadMedidaImportProcessor(_parserResolver, _repository, _connectionFactory, NullLogger<UnidadMedidaImportProcessor>.Instance);
        var fila = CrearFila(1, "Codigo", "UM-001", "Nombre", "Kilogramo", "Activo", "true");

        var dto = processor.MapearFila(fila);

        Assert.That(dto._Fila, Is.EqualTo(1));
        Assert.That(dto.Codigo, Is.EqualTo("UM-001"));
        Assert.That(dto.Nombre, Is.EqualTo("Kilogramo"));
        Assert.That(dto.Activo, Is.True);
    }

    [Test]
    public void UnidadMedida_SinColumnaActivo_DefaultTrue()
    {
        var processor = new UnidadMedidaImportProcessor(_parserResolver, _repository, _connectionFactory, NullLogger<UnidadMedidaImportProcessor>.Instance);
        var fila = CrearFila(1, "Codigo", "UM-001", "Nombre", "Kg");

        var dto = processor.MapearFila(fila);

        Assert.That(dto.Activo, Is.True);
    }

    [Test]
    public void UnidadMedida_CodigoVacio_LanzaKeyNotFound()
    {
        var processor = new UnidadMedidaImportProcessor(_parserResolver, _repository, _connectionFactory, NullLogger<UnidadMedidaImportProcessor>.Instance);
        var fila = CrearFila(1, "Codigo", "  ", "Nombre", "Kg");

        var ex = Assert.Throws<KeyNotFoundException>(() => processor.MapearFila(fila))!;
        Assert.That(ex.Message, Does.Contain("Codigo"));
    }

    [Test]
    public void UnidadMedida_NombreVacio_LanzaKeyNotFound()
    {
        var processor = new UnidadMedidaImportProcessor(_parserResolver, _repository, _connectionFactory, NullLogger<UnidadMedidaImportProcessor>.Instance);
        var fila = CrearFila(1, "Codigo", "UM-001", "Nombre", "");

        var ex = Assert.Throws<KeyNotFoundException>(() => processor.MapearFila(fila))!;
        Assert.That(ex.Message, Does.Contain("Nombre"));
    }

    [Test]
    public void UnidadMedida_ActivoInvalido_LanzaFormatException()
    {
        var processor = new UnidadMedidaImportProcessor(_parserResolver, _repository, _connectionFactory, NullLogger<UnidadMedidaImportProcessor>.Instance);
        var fila = CrearFila(1, "Codigo", "UM-001", "Nombre", "Kg", "Activo", "yesyes");

        Assert.Throws<FormatException>(() => processor.MapearFila(fila));
    }

    // =========================================================================
    // EspecialidadImportProcessor
    // =========================================================================

    [Test]
    public void Especialidad_FilaValida_DevuelveDtoConDatosCorrectos()
    {
        var processor = new EspecialidadImportProcessor(_parserResolver, _repository, _connectionFactory, NullLogger<EspecialidadImportProcessor>.Instance);
        var fila = CrearFila(1,
            "Nombre", "ALBAÑILERIA",
            "Descripcion", "Trabajos de obra",
            "Activo", "true");

        var dto = processor.MapearFila(fila);

        Assert.That(dto._Fila, Is.EqualTo(1));
        Assert.That(dto.Nombre, Is.EqualTo("ALBAÑILERIA"));
        Assert.That(dto.Descripcion, Is.EqualTo("Trabajos de obra"));
        Assert.That(dto.Activo, Is.True);
    }

    [Test]
    public void Especialidad_DescripcionVacia_EsNull()
    {
        var processor = new EspecialidadImportProcessor(_parserResolver, _repository, _connectionFactory, NullLogger<EspecialidadImportProcessor>.Instance);
        var fila = CrearFila(1, "Nombre", "ELECTRICIDAD");

        var dto = processor.MapearFila(fila);

        Assert.That(dto.Descripcion, Is.Null);
        Assert.That(dto.Activo, Is.True);
    }

    [Test]
    public void Especialidad_NombreVacio_LanzaKeyNotFound()
    {
        var processor = new EspecialidadImportProcessor(_parserResolver, _repository, _connectionFactory, NullLogger<EspecialidadImportProcessor>.Instance);
        var fila = CrearFila(1, "Nombre", "   ");

        var ex = Assert.Throws<KeyNotFoundException>(() => processor.MapearFila(fila))!;
        Assert.That(ex.Message, Does.Contain("Nombre"));
    }

    // =========================================================================
    // MaterialImportProcessor (v2: 4 encabezados amigables)
    // =========================================================================

    [Test]
    public void Material_FilaValida_DevuelveDtoCompleto()
    {
        var processor = new MaterialImportProcessor(
            _parserResolver, _repository, _connectionFactory,
            new StubResolvedorEntidadesService(), NullLogger<MaterialImportProcessor>.Instance);

        var fila = CrearFila(1,
            "Especialidad", "Albañileria",
            "Nombre", "Cemento Portland",
            "UnidadMedida", "Bolsa",
            "Codigo", "MAT-001");

        var dto = processor.MapearFila(fila);

        Assert.That(dto._Fila, Is.EqualTo(1));
        Assert.That(dto.Especialidad, Is.EqualTo("Albañileria"));
        Assert.That(dto.Nombre, Is.EqualTo("Cemento Portland"));
        Assert.That(dto.UnidadMedida, Is.EqualTo("Bolsa"));
        Assert.That(dto.Codigo, Is.EqualTo("MAT-001"));
    }

    [Test]
    public void Material_EspecialidadVacia_LanzaKeyNotFound()
    {
        var processor = new MaterialImportProcessor(
            _parserResolver, _repository, _connectionFactory,
            new StubResolvedorEntidadesService(), NullLogger<MaterialImportProcessor>.Instance);

        var fila = CrearFila(1,
            "Especialidad", "  ",
            "Nombre", "Cemento",
            "UnidadMedida", "Bolsa",
            "Codigo", "MAT-001");

        var ex = Assert.Throws<KeyNotFoundException>(() => processor.MapearFila(fila))!;
        Assert.That(ex.Message, Does.Contain("Especialidad"));
    }

    [Test]
    public void Material_NombreVacio_LanzaKeyNotFound()
    {
        var processor = new MaterialImportProcessor(
            _parserResolver, _repository, _connectionFactory,
            new StubResolvedorEntidadesService(), NullLogger<MaterialImportProcessor>.Instance);

        var fila = CrearFila(1,
            "Especialidad", "Albañileria",
            "Nombre", "",
            "UnidadMedida", "Bolsa",
            "Codigo", "MAT-001");

        var ex = Assert.Throws<KeyNotFoundException>(() => processor.MapearFila(fila))!;
        Assert.That(ex.Message, Does.Contain("Nombre"));
    }

    [Test]
    public void Material_UnidadMedidaVacia_LanzaKeyNotFound()
    {
        var processor = new MaterialImportProcessor(
            _parserResolver, _repository, _connectionFactory,
            new StubResolvedorEntidadesService(), NullLogger<MaterialImportProcessor>.Instance);

        var fila = CrearFila(1,
            "Especialidad", "Albañileria",
            "Nombre", "Cemento",
            "UnidadMedida", "  ",
            "Codigo", "MAT-001");

        var ex = Assert.Throws<KeyNotFoundException>(() => processor.MapearFila(fila))!;
        Assert.That(ex.Message, Does.Contain("UnidadMedida"));
    }

    [Test]
    public void Material_CodigoVacio_LanzaKeyNotFound_EnV2EsObligatorio()
    {
        // En v2 el codigo NO se autogenera: si viene vacio, la fila se rechaza.
        var processor = new MaterialImportProcessor(
            _parserResolver, _repository, _connectionFactory,
            new StubResolvedorEntidadesService(), NullLogger<MaterialImportProcessor>.Instance);

        var fila = CrearFila(1,
            "Especialidad", "Albañileria",
            "Nombre", "Cemento",
            "UnidadMedida", "Bolsa",
            "Codigo", "");

        var ex = Assert.Throws<KeyNotFoundException>(() => processor.MapearFila(fila))!;
        Assert.That(ex.Message, Does.Contain("Codigo"));
    }

    [Test]
    public void Material_EncabezadosRequeridos_Contiene4NombresAmigables()
    {
        // Verifica que la plantilla exige los 4 encabezados visibles al usuario
        // (Especialidad, Nombre, UnidadMedida, Codigo), no los IDs tecnicos.
        // EncabezadosRequeridos es protected, asi que usamos una subclase de
        // prueba para exponerlo sin contaminar la API publica.
        var processor = new TestMaterialImportProcessor(
            _parserResolver, _repository, _connectionFactory,
            new StubResolvedorEntidadesService(), NullLogger<MaterialImportProcessor>.Instance);

        var encabezados = processor.GetEncabezadosRequeridos();

        Assert.That(encabezados.Length, Is.EqualTo(4));
        Assert.That(encabezados, Does.Contain("Especialidad"));
        Assert.That(encabezados, Does.Contain("Nombre"));
        Assert.That(encabezados, Does.Contain("UnidadMedida"));
        Assert.That(encabezados, Does.Contain("Codigo"));
    }

    // =========================================================================
    // ProveedorImportProcessor
    // =========================================================================

    [Test]
    public void Proveedor_FilaValida_DevuelveDtoCompleto()
    {
        var processor = new ProveedorImportProcessor(_parserResolver, _repository, _connectionFactory, NullLogger<ProveedorImportProcessor>.Instance);
        var fila = CrearFila(1,
            "RazonSocial", "ACG EDIFICACIONES",
            "Ruc", "20601997291",
            "Contacto", "Juan Perez",
            "Telefono", "999888777",
            "Correo", "a@b.com",
            "Banco", "BCP",
            "TrabajamosConProveedor", "SI",
            "Activo", "true");

        var dto = processor.MapearFila(fila);

        Assert.That(dto._Fila, Is.EqualTo(1));
        Assert.That(dto.RazonSocial, Is.EqualTo("ACG EDIFICACIONES"));
        Assert.That(dto.Ruc, Is.EqualTo("20601997291"));
        Assert.That(dto.Contacto, Is.EqualTo("Juan Perez"));
        Assert.That(dto.Banco, Is.EqualTo("BCP"));
        Assert.That(dto.TrabajamosConProveedor, Is.EqualTo("SI"));
        Assert.That(dto.Activo, Is.True);
    }

    [Test]
    public void Proveedor_SoloRequeridos_DevuelveDtoConOpcionalesEnNull()
    {
        var processor = new ProveedorImportProcessor(_parserResolver, _repository, _connectionFactory, NullLogger<ProveedorImportProcessor>.Instance);
        var fila = CrearFila(1, "RazonSocial", "X S.A.C", "Ruc", "20123456789");

        var dto = processor.MapearFila(fila);

        Assert.That(dto.Contacto, Is.Null);
        Assert.That(dto.Banco, Is.Null);
        Assert.That(dto.Activo, Is.True);
    }

    [Test]
    public void Proveedor_RazonSocialVacia_LanzaKeyNotFound()
    {
        var processor = new ProveedorImportProcessor(_parserResolver, _repository, _connectionFactory, NullLogger<ProveedorImportProcessor>.Instance);
        var fila = CrearFila(1, "RazonSocial", "  ", "Ruc", "20123456789");

        var ex = Assert.Throws<KeyNotFoundException>(() => processor.MapearFila(fila))!;
        Assert.That(ex.Message, Does.Contain("RazonSocial"));
    }

    [Test]
    public void Proveedor_RucVacio_LanzaKeyNotFound()
    {
        var processor = new ProveedorImportProcessor(_parserResolver, _repository, _connectionFactory, NullLogger<ProveedorImportProcessor>.Instance);
        var fila = CrearFila(1, "RazonSocial", "X", "Ruc", "");

        var ex = Assert.Throws<KeyNotFoundException>(() => processor.MapearFila(fila))!;
        Assert.That(ex.Message, Does.Contain("Ruc"));
    }

    // =========================================================================
    // ProveedorGastoAdministrativoImportProcessor
    // =========================================================================

    [Test]
    public void ProveedorGasto_FilaValidaConIdCategoria_DevuelveDtoCompleto()
    {
        var processor = new ProveedorGastoAdministrativoImportProcessor(_parserResolver, _repository, _connectionFactory, NullLogger<ProveedorGastoAdministrativoImportProcessor>.Instance);
        var fila = CrearFila(1,
            "RazonSocial", "PROVEEDOR X",
            "Ruc", "20123456789",
            "IdCategoriaGasto", "1",
            "Activo", "false");

        var dto = processor.MapearFila(fila);

        Assert.That(dto._Fila, Is.EqualTo(1));
        Assert.That(dto.RazonSocial, Is.EqualTo("PROVEEDOR X"));
        Assert.That(dto.Ruc, Is.EqualTo("20123456789"));
        Assert.That(dto.IdCategoriaGasto, Is.EqualTo(1));
        Assert.That(dto.Activo, Is.False);
    }

    [Test]
    public void ProveedorGasto_IdCategoriaInvalido_LanzaFormatException()
    {
        var processor = new ProveedorGastoAdministrativoImportProcessor(_parserResolver, _repository, _connectionFactory, NullLogger<ProveedorGastoAdministrativoImportProcessor>.Instance);
        var fila = CrearFila(1,
            "RazonSocial", "X",
            "IdCategoriaGasto", "no-es-numero");

        Assert.Throws<FormatException>(() => processor.MapearFila(fila));
    }

    [Test]
    public void ProveedorGasto_RazonSocialVacia_LanzaKeyNotFound()
    {
        var processor = new ProveedorGastoAdministrativoImportProcessor(_parserResolver, _repository, _connectionFactory, NullLogger<ProveedorGastoAdministrativoImportProcessor>.Instance);
        var fila = CrearFila(1, "RazonSocial", "");

        var ex = Assert.Throws<KeyNotFoundException>(() => processor.MapearFila(fila))!;
        Assert.That(ex.Message, Does.Contain("RazonSocial"));
    }

    // =========================================================================
    // ProveedorTerrenoImportProcessor
    // =========================================================================

    [Test]
    public void ProveedorTerreno_FilaValida_DevuelveDtoCompleto()
    {
        var processor = new ProveedorTerrenoImportProcessor(_parserResolver, _repository, _connectionFactory, NullLogger<ProveedorTerrenoImportProcessor>.Instance);
        var fila = CrearFila(1,
            "RazonSocial", "TERRENO S.A",
            "Ruc", "20123456789",
            "Telefono", "999111",
            "Activo", "true");

        var dto = processor.MapearFila(fila);

        Assert.That(dto._Fila, Is.EqualTo(1));
        Assert.That(dto.RazonSocial, Is.EqualTo("TERRENO S.A"));
        Assert.That(dto.Telefono, Is.EqualTo("999111"));
        Assert.That(dto.Activo, Is.True);
    }

    [Test]
    public void ProveedorTerreno_RazonSocialVacia_LanzaKeyNotFound()
    {
        var processor = new ProveedorTerrenoImportProcessor(_parserResolver, _repository, _connectionFactory, NullLogger<ProveedorTerrenoImportProcessor>.Instance);
        var fila = CrearFila(1, "RazonSocial", "  ");

        var ex = Assert.Throws<KeyNotFoundException>(() => processor.MapearFila(fila))!;
        Assert.That(ex.Message, Does.Contain("RazonSocial"));
    }

    // =========================================================================
    // CategoriaGastoImportProcessor
    // =========================================================================

    [Test]
    public void CategoriaGasto_FilaValida_DevuelveDtoCompleto()
    {
        var processor = new CategoriaGastoImportProcessor(_parserResolver, _repository, _connectionFactory, NullLogger<CategoriaGastoImportProcessor>.Instance);
        var fila = CrearFila(1, "Nombre", "MARKETING Y VENTAS", "Activo", "true");

        var dto = processor.MapearFila(fila);

        Assert.That(dto._Fila, Is.EqualTo(1));
        Assert.That(dto.Nombre, Is.EqualTo("MARKETING Y VENTAS"));
        Assert.That(dto.Activo, Is.True);
    }

    [Test]
    public void CategoriaGasto_NombreVacio_LanzaKeyNotFound()
    {
        var processor = new CategoriaGastoImportProcessor(_parserResolver, _repository, _connectionFactory, NullLogger<CategoriaGastoImportProcessor>.Instance);
        var fila = CrearFila(1, "Nombre", "");

        var ex = Assert.Throws<KeyNotFoundException>(() => processor.MapearFila(fila))!;
        Assert.That(ex.Message, Does.Contain("Nombre"));
    }

    // =========================================================================
    // Verificacion transversal: Modulo + nombres de SP y TVP
    // =========================================================================

    [Test]
    public void TodosLosModulos_TienenModuloSpYTvpAsignados()
    {
        var stubResolvedor = new StubResolvedorEntidadesService();
        Assert.Multiple(() =>
        {
            Assert.That(new UnidadMedidaImportProcessor(_parserResolver, _repository, _connectionFactory, NullLogger<UnidadMedidaImportProcessor>.Instance).Modulo, Is.EqualTo("unidad-medida"));
            Assert.That(new EspecialidadImportProcessor(_parserResolver, _repository, _connectionFactory, NullLogger<EspecialidadImportProcessor>.Instance).Modulo, Is.EqualTo("especialidad"));
            Assert.That(new MaterialImportProcessor(_parserResolver, _repository, _connectionFactory, stubResolvedor, NullLogger<MaterialImportProcessor>.Instance).Modulo, Is.EqualTo("material"));
            Assert.That(new ProveedorImportProcessor(_parserResolver, _repository, _connectionFactory, NullLogger<ProveedorImportProcessor>.Instance).Modulo, Is.EqualTo("proveedor"));
            Assert.That(new ProveedorGastoAdministrativoImportProcessor(_parserResolver, _repository, _connectionFactory, NullLogger<ProveedorGastoAdministrativoImportProcessor>.Instance).Modulo, Is.EqualTo("proveedor-gasto"));
            Assert.That(new ProveedorTerrenoImportProcessor(_parserResolver, _repository, _connectionFactory, NullLogger<ProveedorTerrenoImportProcessor>.Instance).Modulo, Is.EqualTo("proveedor-terreno"));
            Assert.That(new CategoriaGastoImportProcessor(_parserResolver, _repository, _connectionFactory, NullLogger<CategoriaGastoImportProcessor>.Instance).Modulo, Is.EqualTo("categoria-gasto"));
        });
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    /// <summary>
    /// Construye un <see cref="SpreadsheetRow"/> a partir de pares (columna, valor).
    /// Los pares se interpretan como una secuencia plana: columna1, valor1, columna2, valor2, ...
    /// </summary>
    private static SpreadsheetRow CrearFila(int numeroFila, params string[] paresColumnaValor)
    {
        if (paresColumnaValor.Length % 2 != 0)
            throw new ArgumentException("paresColumnaValor debe tener longitud par", nameof(paresColumnaValor));

        var celdas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < paresColumnaValor.Length; i += 2)
            celdas[paresColumnaValor[i]] = paresColumnaValor[i + 1];

        return new SpreadsheetRow(numeroFila, celdas);
    }

    private sealed class StubRepository : IImportRepository
    {
        public Task<int> ImportAsync<TDto>(
            string spName, string tvpTypeName, IEnumerable<TDto> dtos,
            IDbConnection connection, IDbTransaction? transaction,
            object? extraParameters = null, CancellationToken ct = default) where TDto : class
            => Task.FromResult(0);
    }

    private sealed class StubConnectionFactory : IDbConnectionFactory
    {
        public IDbConnection CreateConnection() => throw new NotSupportedException("Stub usado solo para construir processors en unit tests.");
    }

    /// <summary>
    /// Subclase de prueba que expone <c>EncabezadosRequeridos</c> como public.
    /// El proyecto no usa Moq; la herencia es la forma de acceder a miembros
    /// protected desde los tests sin contaminar la API publica del processor.
    /// </summary>
    private sealed class TestMaterialImportProcessor : MaterialImportProcessor
    {
        public TestMaterialImportProcessor(
            FileParserResolver parserResolver,
            IImportRepository repository,
            IDbConnectionFactory connectionFactory,
            ResolvedorEntidadesService resolvedor,
            Microsoft.Extensions.Logging.ILogger<MaterialImportProcessor> logger)
            : base(parserResolver, repository, connectionFactory, resolvedor, logger)
        {
        }

        public string[] GetEncabezadosRequeridos() => EncabezadosRequeridos;
    }
}
