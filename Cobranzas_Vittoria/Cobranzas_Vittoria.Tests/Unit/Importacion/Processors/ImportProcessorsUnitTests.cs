using Cobranzas_Vittoria.Application.Importacion.Dtos;
using Cobranzas_Vittoria.Application.Importacion.Parsers;
using Cobranzas_Vittoria.Application.Importacion.Persistence;
using Cobranzas_Vittoria.Application.Importacion.Processors;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Domain.Importacion;
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
        var processor = new UnidadMedidaImportProcessor(_parserResolver, _repository, _connectionFactory);
        var fila = CrearFila(1, "Codigo", "UM-001", "Nombre", "Kilogramo", "Activo", "true");

        var dto = InvocarMapear(processor, fila);

        Assert.That(dto._Fila, Is.EqualTo(1));
        Assert.That(dto.Codigo, Is.EqualTo("UM-001"));
        Assert.That(dto.Nombre, Is.EqualTo("Kilogramo"));
        Assert.That(dto.Activo, Is.True);
    }

    [Test]
    public void UnidadMedida_SinColumnaActivo_DefaultTrue()
    {
        var processor = new UnidadMedidaImportProcessor(_parserResolver, _repository, _connectionFactory);
        var fila = CrearFila(1, "Codigo", "UM-001", "Nombre", "Kg");

        var dto = InvocarMapear(processor, fila);

        Assert.That(dto.Activo, Is.True);
    }

    [Test]
    public void UnidadMedida_CodigoVacio_LanzaKeyNotFound()
    {
        var processor = new UnidadMedidaImportProcessor(_parserResolver, _repository, _connectionFactory);
        var fila = CrearFila(1, "Codigo", "  ", "Nombre", "Kg");

        var ex = Assert.Throws<KeyNotFoundException>(() => InvocarMapear(processor, fila))!;
        Assert.That(ex.Message, Does.Contain("Codigo"));
    }

    [Test]
    public void UnidadMedida_NombreVacio_LanzaKeyNotFound()
    {
        var processor = new UnidadMedidaImportProcessor(_parserResolver, _repository, _connectionFactory);
        var fila = CrearFila(1, "Codigo", "UM-001", "Nombre", "");

        var ex = Assert.Throws<KeyNotFoundException>(() => InvocarMapear(processor, fila))!;
        Assert.That(ex.Message, Does.Contain("Nombre"));
    }

    [Test]
    public void UnidadMedida_ActivoInvalido_LanzaFormatException()
    {
        var processor = new UnidadMedidaImportProcessor(_parserResolver, _repository, _connectionFactory);
        var fila = CrearFila(1, "Codigo", "UM-001", "Nombre", "Kg", "Activo", "yesyes");

        Assert.Throws<FormatException>(() => InvocarMapear(processor, fila));
    }

    // =========================================================================
    // EspecialidadImportProcessor
    // =========================================================================

    [Test]
    public void Especialidad_FilaValida_DevuelveDtoConDatosCorrectos()
    {
        var processor = new EspecialidadImportProcessor(_parserResolver, _repository, _connectionFactory);
        var fila = CrearFila(1,
            "Nombre", "ALBAÑILERIA",
            "Descripcion", "Trabajos de obra",
            "Activo", "true");

        var dto = InvocarMapear(processor, fila);

        Assert.That(dto._Fila, Is.EqualTo(1));
        Assert.That(dto.Nombre, Is.EqualTo("ALBAÑILERIA"));
        Assert.That(dto.Descripcion, Is.EqualTo("Trabajos de obra"));
        Assert.That(dto.Activo, Is.True);
    }

    [Test]
    public void Especialidad_DescripcionVacia_EsNull()
    {
        var processor = new EspecialidadImportProcessor(_parserResolver, _repository, _connectionFactory);
        var fila = CrearFila(1, "Nombre", "ELECTRICIDAD");

        var dto = InvocarMapear(processor, fila);

        Assert.That(dto.Descripcion, Is.Null);
        Assert.That(dto.Activo, Is.True);
    }

    [Test]
    public void Especialidad_NombreVacio_LanzaKeyNotFound()
    {
        var processor = new EspecialidadImportProcessor(_parserResolver, _repository, _connectionFactory);
        var fila = CrearFila(1, "Nombre", "   ");

        var ex = Assert.Throws<KeyNotFoundException>(() => InvocarMapear(processor, fila))!;
        Assert.That(ex.Message, Does.Contain("Nombre"));
    }

    // =========================================================================
    // MaterialImportProcessor
    // =========================================================================

    [Test]
    public void Material_FilaValida_ConCodigo_DevuelveDtoCompleto()
    {
        var processor = new MaterialImportProcessor(_parserResolver, _repository, _connectionFactory);
        var fila = CrearFila(1,
            "IdEspecialidad", "1",
            "Codigo", "MAT-9999",
            "Descripcion", "Cemento",
            "UnidadMedida", "BOL",
            "StockMinimo", "10.5",
            "Activo", "true",
            "IdUnidadMedida", "1",
            "CodigoProveedor", "PROV-1");

        var dto = InvocarMapear(processor, fila);

        Assert.That(dto._Fila, Is.EqualTo(1));
        Assert.That(dto.IdEspecialidad, Is.EqualTo(1));
        Assert.That(dto.Codigo, Is.EqualTo("MAT-9999"));
        Assert.That(dto.Descripcion, Is.EqualTo("Cemento"));
        Assert.That(dto.UnidadMedida, Is.EqualTo("BOL"));
        Assert.That(dto.StockMinimo, Is.EqualTo(10.5m));
        Assert.That(dto.IdUnidadMedida, Is.EqualTo(1));
        Assert.That(dto.CodigoProveedor, Is.EqualTo("PROV-1"));
    }

    [Test]
    public void Material_FilaSinCodigo_CodigoEsNull_ParaAutogenerarEnSp()
    {
        var processor = new MaterialImportProcessor(_parserResolver, _repository, _connectionFactory);
        var fila = CrearFila(1,
            "IdEspecialidad", "1",
            "Descripcion", "Arena",
            "UnidadMedida", "M3");

        var dto = InvocarMapear(processor, fila);

        Assert.That(dto.Codigo, Is.Null);
        Assert.That(dto.StockMinimo, Is.EqualTo(0m));
        Assert.That(dto.IdUnidadMedida, Is.Null);
    }

    [Test]
    public void Material_IdEspecialidadNoEntero_LanzaFormatException()
    {
        var processor = new MaterialImportProcessor(_parserResolver, _repository, _connectionFactory);
        var fila = CrearFila(1,
            "IdEspecialidad", "abc",
            "Descripcion", "X",
            "UnidadMedida", "UND");

        Assert.Throws<FormatException>(() => InvocarMapear(processor, fila));
    }

    [Test]
    public void Material_StockMinimoInvalido_LanzaFormatException()
    {
        var processor = new MaterialImportProcessor(_parserResolver, _repository, _connectionFactory);
        var fila = CrearFila(1,
            "IdEspecialidad", "1",
            "Descripcion", "X",
            "UnidadMedida", "UND",
            "StockMinimo", "no-es-numero");

        Assert.Throws<FormatException>(() => InvocarMapear(processor, fila));
    }

    [Test]
    public void Material_DescripcionVacia_LanzaKeyNotFound()
    {
        var processor = new MaterialImportProcessor(_parserResolver, _repository, _connectionFactory);
        var fila = CrearFila(1,
            "IdEspecialidad", "1",
            "Descripcion", "",
            "UnidadMedida", "UND");

        var ex = Assert.Throws<KeyNotFoundException>(() => InvocarMapear(processor, fila))!;
        Assert.That(ex.Message, Does.Contain("Descripcion"));
    }

    // =========================================================================
    // ProveedorImportProcessor
    // =========================================================================

    [Test]
    public void Proveedor_FilaValida_DevuelveDtoCompleto()
    {
        var processor = new ProveedorImportProcessor(_parserResolver, _repository, _connectionFactory);
        var fila = CrearFila(1,
            "RazonSocial", "ACG EDIFICACIONES",
            "Ruc", "20601997291",
            "Contacto", "Juan Perez",
            "Telefono", "999888777",
            "Correo", "a@b.com",
            "Banco", "BCP",
            "TrabajamosConProveedor", "SI",
            "Activo", "true");

        var dto = InvocarMapear(processor, fila);

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
        var processor = new ProveedorImportProcessor(_parserResolver, _repository, _connectionFactory);
        var fila = CrearFila(1, "RazonSocial", "X S.A.C", "Ruc", "20123456789");

        var dto = InvocarMapear(processor, fila);

        Assert.That(dto.Contacto, Is.Null);
        Assert.That(dto.Banco, Is.Null);
        Assert.That(dto.Activo, Is.True);
    }

    [Test]
    public void Proveedor_RazonSocialVacia_LanzaKeyNotFound()
    {
        var processor = new ProveedorImportProcessor(_parserResolver, _repository, _connectionFactory);
        var fila = CrearFila(1, "RazonSocial", "  ", "Ruc", "20123456789");

        var ex = Assert.Throws<KeyNotFoundException>(() => InvocarMapear(processor, fila))!;
        Assert.That(ex.Message, Does.Contain("RazonSocial"));
    }

    [Test]
    public void Proveedor_RucVacio_LanzaKeyNotFound()
    {
        var processor = new ProveedorImportProcessor(_parserResolver, _repository, _connectionFactory);
        var fila = CrearFila(1, "RazonSocial", "X", "Ruc", "");

        var ex = Assert.Throws<KeyNotFoundException>(() => InvocarMapear(processor, fila))!;
        Assert.That(ex.Message, Does.Contain("Ruc"));
    }

    // =========================================================================
    // ProveedorGastoAdministrativoImportProcessor
    // =========================================================================

    [Test]
    public void ProveedorGasto_FilaValidaConIdCategoria_DevuelveDtoCompleto()
    {
        var processor = new ProveedorGastoAdministrativoImportProcessor(_parserResolver, _repository, _connectionFactory);
        var fila = CrearFila(1,
            "RazonSocial", "PROVEEDOR X",
            "Ruc", "20123456789",
            "IdCategoriaGasto", "1",
            "Activo", "false");

        var dto = InvocarMapear(processor, fila);

        Assert.That(dto._Fila, Is.EqualTo(1));
        Assert.That(dto.RazonSocial, Is.EqualTo("PROVEEDOR X"));
        Assert.That(dto.Ruc, Is.EqualTo("20123456789"));
        Assert.That(dto.IdCategoriaGasto, Is.EqualTo(1));
        Assert.That(dto.Activo, Is.False);
    }

    [Test]
    public void ProveedorGasto_IdCategoriaInvalido_LanzaFormatException()
    {
        var processor = new ProveedorGastoAdministrativoImportProcessor(_parserResolver, _repository, _connectionFactory);
        var fila = CrearFila(1,
            "RazonSocial", "X",
            "IdCategoriaGasto", "no-es-numero");

        Assert.Throws<FormatException>(() => InvocarMapear(processor, fila));
    }

    [Test]
    public void ProveedorGasto_RazonSocialVacia_LanzaKeyNotFound()
    {
        var processor = new ProveedorGastoAdministrativoImportProcessor(_parserResolver, _repository, _connectionFactory);
        var fila = CrearFila(1, "RazonSocial", "");

        var ex = Assert.Throws<KeyNotFoundException>(() => InvocarMapear(processor, fila))!;
        Assert.That(ex.Message, Does.Contain("RazonSocial"));
    }

    // =========================================================================
    // ProveedorTerrenoImportProcessor
    // =========================================================================

    [Test]
    public void ProveedorTerreno_FilaValida_DevuelveDtoCompleto()
    {
        var processor = new ProveedorTerrenoImportProcessor(_parserResolver, _repository, _connectionFactory);
        var fila = CrearFila(1,
            "RazonSocial", "TERRENO S.A",
            "Ruc", "20123456789",
            "Telefono", "999111",
            "Activo", "true");

        var dto = InvocarMapear(processor, fila);

        Assert.That(dto._Fila, Is.EqualTo(1));
        Assert.That(dto.RazonSocial, Is.EqualTo("TERRENO S.A"));
        Assert.That(dto.Telefono, Is.EqualTo("999111"));
        Assert.That(dto.Activo, Is.True);
    }

    [Test]
    public void ProveedorTerreno_RazonSocialVacia_LanzaKeyNotFound()
    {
        var processor = new ProveedorTerrenoImportProcessor(_parserResolver, _repository, _connectionFactory);
        var fila = CrearFila(1, "RazonSocial", "  ");

        var ex = Assert.Throws<KeyNotFoundException>(() => InvocarMapear(processor, fila))!;
        Assert.That(ex.Message, Does.Contain("RazonSocial"));
    }

    // =========================================================================
    // CategoriaGastoImportProcessor
    // =========================================================================

    [Test]
    public void CategoriaGasto_FilaValida_DevuelveDtoCompleto()
    {
        var processor = new CategoriaGastoImportProcessor(_parserResolver, _repository, _connectionFactory);
        var fila = CrearFila(1, "Nombre", "MARKETING Y VENTAS", "Activo", "true");

        var dto = InvocarMapear(processor, fila);

        Assert.That(dto._Fila, Is.EqualTo(1));
        Assert.That(dto.Nombre, Is.EqualTo("MARKETING Y VENTAS"));
        Assert.That(dto.Activo, Is.True);
    }

    [Test]
    public void CategoriaGasto_NombreVacio_LanzaKeyNotFound()
    {
        var processor = new CategoriaGastoImportProcessor(_parserResolver, _repository, _connectionFactory);
        var fila = CrearFila(1, "Nombre", "");

        var ex = Assert.Throws<KeyNotFoundException>(() => InvocarMapear(processor, fila))!;
        Assert.That(ex.Message, Does.Contain("Nombre"));
    }

    // =========================================================================
    // Verificacion transversal: Modulo + nombres de SP y TVP
    // =========================================================================

    [Test]
    public void TodosLosModulos_TienenModuloSpYTvpAsignados()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new UnidadMedidaImportProcessor(_parserResolver, _repository, _connectionFactory).Modulo, Is.EqualTo("unidad-medida"));
            Assert.That(new EspecialidadImportProcessor(_parserResolver, _repository, _connectionFactory).Modulo, Is.EqualTo("especialidad"));
            Assert.That(new MaterialImportProcessor(_parserResolver, _repository, _connectionFactory).Modulo, Is.EqualTo("material"));
            Assert.That(new ProveedorImportProcessor(_parserResolver, _repository, _connectionFactory).Modulo, Is.EqualTo("proveedor"));
            Assert.That(new ProveedorGastoAdministrativoImportProcessor(_parserResolver, _repository, _connectionFactory).Modulo, Is.EqualTo("proveedor-gasto"));
            Assert.That(new ProveedorTerrenoImportProcessor(_parserResolver, _repository, _connectionFactory).Modulo, Is.EqualTo("proveedor-terreno"));
            Assert.That(new CategoriaGastoImportProcessor(_parserResolver, _repository, _connectionFactory).Modulo, Is.EqualTo("categoria-gasto"));
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

    /// <summary>
    /// Invoca el metodo protegido <c>MapearFila</c> por reflection y devuelve
    /// <c>dynamic</c> para evitar tener que especificar el tipo generico en
    /// cada test. Si <c>MapearFila</c> lanza una excepcion, esta se desenvuelve
    /// del <see cref="System.Reflection.TargetInvocationException"/> que agrega
    /// <c>MethodInfo.Invoke</c> para preservar el tipo original.
    /// </summary>
    private static dynamic InvocarMapear(IImportProcessor processor, SpreadsheetRow fila)
    {
        var metodo = processor.GetType().GetMethod(
            "MapearFila",
            BindingFlagsInstance | BindingFlagsNonPublic)!;
        try
        {
            return metodo.Invoke(processor, new object[] { fila })!;
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
        {
            // Re-throw la excepcion interna con su stack trace original,
            // tal como haria un await/try en codigo de produccion.
            var exceptionProp = ex.InnerException.GetType().GetProperty("InnerException", BindingFlagsInstance | BindingFlagsNonPublic);
            _ = exceptionProp; // (no usado; solo para documentar la intencion)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw; // inalcanzable, el Throw() anterior ya es una excepcion
        }
    }

    // Aliases locales para no chocar con System.Reflection.BindingFlags.
    private const System.Reflection.BindingFlags BindingFlagsInstance = System.Reflection.BindingFlags.Instance;
    private const System.Reflection.BindingFlags BindingFlagsNonPublic = System.Reflection.BindingFlags.NonPublic;

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
}
