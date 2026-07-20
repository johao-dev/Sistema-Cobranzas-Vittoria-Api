using Cobranzas_Vittoria.Application.Importacion;
using Cobranzas_Vittoria.Application.Importacion.Dtos;
using Cobranzas_Vittoria.Application.Importacion.Excepciones;
using Cobranzas_Vittoria.Application.Importacion.Parsers;
using Cobranzas_Vittoria.Application.Importacion.Persistence;
using Cobranzas_Vittoria.Application.Importacion.Processors;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Domain.Importacion;
using Microsoft.Extensions.Logging;

namespace Cobranzas_Vittoria.Tests.Unit.Importacion.Processors.Common;

/// <summary>
/// Subclase concreta de <see cref="ImportProcessorBase{TDto}"/> usada SOLO en
/// pruebas unitarias. Expone los miembros <c>protected</c> para poder
/// validarlos de forma aislada sin necesidad de parsers, conexiones ni SPs.
///
/// Mantiene un mapeo trivial (cada fila genera un DTO con un contador en Codigo
/// y el primer campo string como Nombre). Para probar mapeos reales de cada
/// modulo se usan los tests unitarios de cada processor concreto.
/// </summary>
internal sealed class TestImportProcessor : ImportProcessorBase<UnidadMedidaImportDto>
{
    /// <summary>Bandera para que <see cref="MapearFila"/> lance <see cref="KeyNotFoundException"/> en la fila indicada (1-based).</summary>
    public int? LanzarKeyNotFoundEnFila { get; set; }

    /// <summary>Bandera para que <see cref="MapearFila"/> lance <see cref="FormatException"/> en la fila indicada (1-based).</summary>
    public int? LanzarFormatEnFila { get; set; }

    /// <summary>Bandera para que <see cref="MapearFila"/> lance <see cref="DatosInvalidosException"/> en la fila indicada (1-based).</summary>
    public int? LanzarDatosInvalidosEnFila { get; set; }

    public TestImportProcessor(
        FileParserResolver parserResolver,
        IImportRepository repository,
        IDbConnectionFactory connectionFactory,
        ILogger logger)
        : base(parserResolver, repository, connectionFactory, logger) { }

    public const string TestModulo = "test-modulo";

    public override string Modulo => TestModulo;
    protected override string SpName => "maestra.usp_Test_CargaMasiva";
    protected override string TvpTypeName => "maestra.TVP_Test";
    protected override string[] EncabezadosRequeridos => new[] { "Codigo", "Nombre" };

    // Exponer protected para tests
    public void LlamarValidarEstructura(IReadOnlyList<SpreadsheetRow> filas) => ValidarEstructura(filas);
    public void LlamarMapearFilas(IReadOnlyList<SpreadsheetRow> filas, List<UnidadMedidaImportDto> dtos, List<DetalleErrorFila> errores)
        => MapearFilas(filas, dtos, errores);

    /// <summary>
    /// Mapeo trivial para tests: si la fila tiene un campo "ThrowType" con valor
    /// "knf" lanza KeyNotFoundException; "fmt" lanza FormatException; "dex"
    /// lanza DatosInvalidosException. Si no, genera un DTO con Codigo = fila 1,
    /// Nombre = celda "Nombre", Activo = true.
    /// </summary>
    internal override UnidadMedidaImportDto MapearFila(SpreadsheetRow fila)
    {
        if (LanzarKeyNotFoundEnFila == fila.NumeroFila)
            throw new KeyNotFoundException($"Campo requerido ausente en fila {fila.NumeroFila}.");
        if (LanzarFormatEnFila == fila.NumeroFila)
            throw new FormatException($"Formato invalido en fila {fila.NumeroFila}.");
        if (LanzarDatosInvalidosEnFila == fila.NumeroFila)
            throw new DatosInvalidosException("Regla de negocio violada", new[]
            {
                new DetalleErrorFila(fila.NumeroFila, "Codigo", CodigosError.Fila.ReglaNegocio, "Codigo debe empezar con PREFIX-")
            });

        var nombre = fila.GetString("Nombre") ?? string.Empty;
        return new UnidadMedidaImportDto
        {
            _Fila = fila.NumeroFila,
            Codigo = $"ROW-{fila.NumeroFila}",
            Nombre = nombre,
            Activo = true
        };
    }
}
