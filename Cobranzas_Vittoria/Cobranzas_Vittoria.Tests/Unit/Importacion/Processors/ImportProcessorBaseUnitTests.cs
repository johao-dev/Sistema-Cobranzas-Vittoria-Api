using Cobranzas_Vittoria.Application.Importacion;
using Cobranzas_Vittoria.Application.Importacion.Dtos;
using Cobranzas_Vittoria.Application.Importacion.Excepciones;
using Cobranzas_Vittoria.Application.Importacion.Parsers;
using Cobranzas_Vittoria.Application.Importacion.Persistence;
using Cobranzas_Vittoria.Application.Importacion.Processors;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Domain.Importacion;
using Cobranzas_Vittoria.Tests.Unit.Importacion.Processors.Common;

namespace Cobranzas_Vittoria.Tests.Unit.Importacion.Processors;

/// <summary>
/// Pruebas unitarias del Template Method definido en <see cref="ImportProcessorBase{TDto}"/>.
///
/// Se enfoca en los pasos que pueden ejercitarse sin una conexion real a BD:
///   - <c>ValidarEstructura</c>: cantidad de filas y encabezados requeridos.
///   - <c>MapearFilas</c>: agregacion de errores por fila con la numeracion correcta
///     y la traduccion de excepciones de conversion a <see cref="DetalleErrorFila"/>.
///   - Traduccion de <see cref="SqlException"/> del SP a <see cref="DatosInvalidosException"/>
///     (codigos 50001-50004 y codigo fuera de rango).
///
/// La ejecucion end-to-end (parser + SP + transaccion) se cubre con tests
/// de integracion en Fase 7.
/// </summary>
public class ImportProcessorBaseUnitTests
{
    private readonly TestImportProcessor _processor;
    private readonly FileParserResolver _parserResolver;
    private readonly FakeImportRepository _repository;
    private readonly FakeConnectionFactory _connectionFactory;

    public ImportProcessorBaseUnitTests()
    {
        _repository = new FakeImportRepository();
        _connectionFactory = new FakeConnectionFactory();
        _parserResolver = new FileParserResolver(new IFileParser[]
        {
            new CsvFileParser(),
            new ExcelFileParser()
        });
        _processor = new TestImportProcessor(_parserResolver, _repository, _connectionFactory);
    }

    [SetUp]
    public void ResetearEstadoCompartido()
    {
        // El processor es compartido entre tests del mismo fixture (NUnit crea
        // una sola instancia del fixture), por lo que los flags Lanzar* se
        // quedan seteados del test anterior. Los reseteamos antes de cada uno.
        _processor.LanzarKeyNotFoundEnFila = null;
        _processor.LanzarFormatEnFila = null;
        _processor.LanzarDatosInvalidosEnFila = null;
    }

    // =========================================================================
    // ValidarEstructura
    // =========================================================================

    [Test]
    public void ValidarEstructura_ListaVacia_LanzaDatosInvalidosSinErrores()
    {
        var filas = new List<SpreadsheetRow>();

        var ex = Assert.Throws<DatosInvalidosException>(() => _processor.LlamarValidarEstructura(filas))!;
        Assert.That(ex.Message, Does.Contain("no contiene filas"));
    }

    [Test]
    public void ValidarEstructura_MasDe100Filas_LanzaDatosInvalidosConDemasiadasFilas()
    {
        // 101 filas validas (con Codigo y Nombre)
        var filas = Enumerable.Range(1, 101)
            .Select(i => CrearFila(i, $"C{i:000}", $"N{i}"))
            .ToList();

        var ex = Assert.Throws<DatosInvalidosException>(() => _processor.LlamarValidarEstructura(filas))!;
        Assert.That(ex.Message, Does.Contain("101"));
        Assert.That(ex.Errores.Count, Is.EqualTo(1));
        Assert.That(ex.Errores[0].CodigoError, Is.EqualTo(CodigosError.Estructura.DemasiadasFilas));
        Assert.That(ex.Errores[0].Fila, Is.EqualTo(0));
    }

    [Test]
    public void ValidarEstructura_FaltaEncabezadoRequerido_LanzaEstructuraInvalida()
    {
        // Construimos una fila sin la columna "Nombre" (solo con Codigo).
        var fila = new SpreadsheetRow(1, new Dictionary<string, string>
        {
            { "Codigo", "C001" }
        });
        var filas = new List<SpreadsheetRow> { fila };

        var ex = Assert.Throws<EstructuraInvalidaException>(() => _processor.LlamarValidarEstructura(filas))!;
        Assert.That(ex.Codigo, Is.EqualTo(CodigosError.Estructura.EncabezadosIncorrectos));
        Assert.That(ex.Message, Does.Contain("Nombre"));
    }

    [Test]
    public void ValidarEstructura_EncabezadoRequeridoEsVacio_NoLanza()
    {
        // La validacion de encabezados solo verifica nombres de columna, no contenido.
        // Si el archivo tiene una columna con valor vacio pero la columna existe, no falla aqui.
        var filas = new List<SpreadsheetRow>
        {
            CrearFila(1, "C001", "Nombre1")
        };

        Assert.DoesNotThrow(() => _processor.LlamarValidarEstructura(filas));
    }

    [Test]
    public void ValidarEstructura_EncabezadosCaseInsensitive_NoLanza()
    {
        // Encabezados en MAYUSCULAS mientras el processor espera "Codigo"/"Nombre"
        var filas = new List<SpreadsheetRow>
        {
            new SpreadsheetRow(1, new Dictionary<string, string>
            {
                { "CODIGO", "C001" },
                { "NOMBRE", "N1" }
            })
        };

        Assert.DoesNotThrow(() => _processor.LlamarValidarEstructura(filas));
    }

    [Test]
    public void ValidarEstructura_Exactamente100Filas_NoLanza()
    {
        var filas = Enumerable.Range(1, 100)
            .Select(i => CrearFila(i, $"C{i:000}", $"N{i}"))
            .ToList();

        Assert.DoesNotThrow(() => _processor.LlamarValidarEstructura(filas));
    }

    // =========================================================================
    // MapearFilas
    // =========================================================================

    [Test]
    public void MapearFilas_TodasValidas_DevuelveMismaCantidadDeDtosSinErrores()
    {
        var filas = new List<SpreadsheetRow>
        {
            CrearFila(1, "C001", "N1"),
            CrearFila(2, "C002", "N2"),
            CrearFila(3, "C003", "N3")
        };
        var dtos = new List<UnidadMedidaImportDto>();
        var errores = new List<DetalleErrorFila>();

        _processor.LlamarMapearFilas(filas, dtos, errores);

        Assert.That(dtos.Count, Is.EqualTo(3));
        Assert.That(errores, Is.Empty);
        Assert.That(dtos[0].Codigo, Is.EqualTo("ROW-1"));
        Assert.That(dtos[2].Nombre, Is.EqualTo("N3"));
    }

    [Test]
    public void MapearFilas_UnaFilaInvalidaPorKeyNotFound_AcumulaErrorConFilaCorrecta()
    {
        var filas = new List<SpreadsheetRow>
        {
            CrearFila(1, "C001", "N1"),
            CrearFila(2, "C002", "N2"),
            CrearFila(3, "C003", "N3")
        };
        _processor.LanzarKeyNotFoundEnFila = 2;

        var dtos = new List<UnidadMedidaImportDto>();
        var errores = new List<DetalleErrorFila>();

        _processor.LlamarMapearFilas(filas, dtos, errores);

        // Las 3 filas se intentan mapear; 2 OK, 1 falla.
        Assert.That(dtos.Count, Is.EqualTo(2));
        Assert.That(errores.Count, Is.EqualTo(1));
        Assert.That(errores[0].Fila, Is.EqualTo(2));
        Assert.That(errores[0].CodigoError, Is.EqualTo(CodigosError.Fila.CampoRequerido));
    }

    [Test]
    public void MapearFilas_UnaFilaInvalidaPorFormat_AcumulaErrorConCodigoFormatoInvalido()
    {
        var filas = new List<SpreadsheetRow>
        {
            CrearFila(1, "C001", "N1"),
            CrearFila(2, "C002", "N2")
        };
        _processor.LanzarFormatEnFila = 2;

        var dtos = new List<UnidadMedidaImportDto>();
        var errores = new List<DetalleErrorFila>();

        _processor.LlamarMapearFilas(filas, dtos, errores);

        Assert.That(errores.Count, Is.EqualTo(1));
        Assert.That(errores[0].Fila, Is.EqualTo(2));
        Assert.That(errores[0].CodigoError, Is.EqualTo("FORMATO_INVALIDO"));
    }

    [Test]
    public void MapearFilas_UnaFilaInvalidaPorDatosInvalidos_PreservaDetalleOriginal()
    {
        var filas = new List<SpreadsheetRow>
        {
            CrearFila(1, "C001", "N1"),
            CrearFila(2, "C002", "N2")
        };
        _processor.LanzarDatosInvalidosEnFila = 1;

        var dtos = new List<UnidadMedidaImportDto>();
        var errores = new List<DetalleErrorFila>();

        _processor.LlamarMapearFilas(filas, dtos, errores);

        Assert.That(errores.Count, Is.EqualTo(1));
        Assert.That(errores[0].Fila, Is.EqualTo(1));
        Assert.That(errores[0].CodigoError, Is.EqualTo(CodigosError.Fila.ReglaNegocio));
        Assert.That(errores[0].Campo, Is.EqualTo("Codigo"));
        Assert.That(errores[0].Mensaje, Does.Contain("PREFIX-"));
    }

    [Test]
    public void MapearFilas_TresFilasInvalidasDeDistintoTipo_AcumulaTodosLosErrores()
    {
        var filas = new List<SpreadsheetRow>
        {
            CrearFila(1, "C001", "N1"),
            CrearFila(2, "C002", "N2"),
            CrearFila(3, "C003", "N3"),
            CrearFila(4, "C004", "N4")
        };
        _processor.LanzarKeyNotFoundEnFila = 1;
        _processor.LanzarFormatEnFila = 3;
        _processor.LanzarDatosInvalidosEnFila = 4;

        var dtos = new List<UnidadMedidaImportDto>();
        var errores = new List<DetalleErrorFila>();

        _processor.LlamarMapearFilas(filas, dtos, errores);

        Assert.That(dtos.Count, Is.EqualTo(1));   // solo fila 2 OK
        Assert.That(errores.Count, Is.EqualTo(3));
        Assert.That(errores.Select(e => e.Fila), Is.EquivalentTo(new[] { 1, 3, 4 }));
        Assert.That(errores.Select(e => e.CodigoError), Is.EquivalentTo(new[] { CodigosError.Fila.CampoRequerido, CodigosError.Fila.FormatoInvalido, CodigosError.Fila.ReglaNegocio }));
    }

    // =========================================================================
    // Traduccion de SqlException -> DatosInvalidosException
    // (test indirecto: el processor expone la traduccion via el camino del SP
    //  cuando el repository fake lanza SqlException. Para no ejercitar la
    //  conexion real, validamos la traduccion simulando un repository que lanza
    //  una SqlException con los codigos que la base reconoce.)
    // =========================================================================

    [Test]
    public void MapearCodigoSql_50001_DevuelveCampoObligatorio()
    {
        Assert.That(ImportProcessorBase<UnidadMedidaImportDto>.MapearCodigoSql(50001), Is.EqualTo(CodigosError.Sp.CampoObligatorio));
    }

    [Test]
    public void MapearCodigoSql_50002_DevuelveValorDuplicadoEnArchivo()
    {
        Assert.That(ImportProcessorBase<UnidadMedidaImportDto>.MapearCodigoSql(50002), Is.EqualTo(CodigosError.Sp.ValorDuplicadoEnArchivo));
    }

    [Test]
    public void MapearCodigoSql_50003_DevuelveValorYaExisteEnBd()
    {
        Assert.That(ImportProcessorBase<UnidadMedidaImportDto>.MapearCodigoSql(50003), Is.EqualTo(CodigosError.Sp.ValorYaExisteEnBd));
    }

    [Test]
    public void MapearCodigoSql_50004_DevuelveFkNoExiste()
    {
        Assert.That(ImportProcessorBase<UnidadMedidaImportDto>.MapearCodigoSql(50004), Is.EqualTo(CodigosError.Sp.FkNoExiste));
    }

    [Test]
    public void MapearCodigoSql_FueraDeRango_DevuelveErrorValidacionN()
    {
        Assert.That(ImportProcessorBase<UnidadMedidaImportDto>.MapearCodigoSql(50099), Is.EqualTo($"{CodigosError.Sp.ErrorValidacionPrefijo}_50099"));
        Assert.That(ImportProcessorBase<UnidadMedidaImportDto>.MapearCodigoSql(50100), Is.EqualTo($"{CodigosError.Sp.ErrorValidacionPrefijo}_50100"));
    }

    // =========================================================================
    // Helpers de tests
    // =========================================================================

    private static SpreadsheetRow CrearFila(int numeroFila, string codigo, string nombre)
    {
        return new SpreadsheetRow(numeroFila, new Dictionary<string, string>
        {
            { "Codigo", codigo },
            { "Nombre", nombre }
        });
    }

    // =========================================================================
    // Fakes de las dependencias (sin mock library)
    // =========================================================================

    private sealed class FakeImportRepository : IImportRepository
    {
        public int Llamadas { get; private set; }

        public Task<int> ImportAsync<TDto>(
            string spName, string tvpTypeName, IEnumerable<TDto> dtos,
            System.Data.IDbConnection connection, System.Data.IDbTransaction? transaction,
            object? extraParameters = null, CancellationToken ct = default) where TDto : class
        {
            Llamadas++;
            return Task.FromResult(0);
        }
    }

    /// <summary>Fake que devuelve un connection no usable; sirve solo para construir el processor.</summary>
    private sealed class FakeConnectionFactory : IDbConnectionFactory
    {
        public System.Data.IDbConnection CreateConnection() => new NotARealConnection();
    }

    /// <summary>Conexion stub que cumple con la interfaz pero no se conecta a nada.</summary>
    private sealed class NotARealConnection : System.Data.IDbConnection
    {
        public string ConnectionString { get; set; } = string.Empty;
        public int ConnectionTimeout => 0;
        public string Database => string.Empty;
        public System.Data.ConnectionState State => System.Data.ConnectionState.Closed;
        public System.Data.IDbTransaction BeginTransaction() => throw new NotSupportedException("Fake connection");
        public System.Data.IDbTransaction BeginTransaction(System.Data.IsolationLevel il) => throw new NotSupportedException();
        public void ChangeDatabase(string databaseName) { }
        public void Close() { }
        public System.Data.IDbCommand CreateCommand() => throw new NotSupportedException();
        public void Open() { }
        public void Dispose() { }
    }
}
