using Cobranzas_Vittoria.Application.Importacion.Dtos;
using Cobranzas_Vittoria.Application.Importacion.Parsers;
using Cobranzas_Vittoria.Application.Importacion.Persistence;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Domain.Importacion;
using Microsoft.Extensions.Logging;

namespace Cobranzas_Vittoria.Application.Importacion.Processors;

/// <summary>
/// Processor de importacion masiva para <c>maestra.CategoriaGasto</c>.
///
/// Encabezados requeridos: <c>Nombre</c>.
/// Opcionales: <c>Activo</c> (default true).
/// </summary>
public class CategoriaGastoImportProcessor : ImportProcessorBase<CategoriaGastoImportDto>
{
    public const string ModuloNombre = "categoria-gasto";

    public CategoriaGastoImportProcessor(
        FileParserResolver parserResolver,
        IImportRepository repository,
        IDbConnectionFactory connectionFactory,
        ILogger<CategoriaGastoImportProcessor> logger)
        : base(parserResolver, repository, connectionFactory, logger) { }

    public override string Modulo => ModuloNombre;

    protected override string SpName => "maestra.usp_CategoriaGasto_CargaMasiva";
    protected override string TvpTypeName => "maestra.TVP_CategoriaGasto";

    protected override string[] EncabezadosRequeridos => new[] { "Nombre" };

    internal override CategoriaGastoImportDto MapearFila(SpreadsheetRow fila)
    {
        var nombre = fila.GetString("Nombre");
        if (string.IsNullOrWhiteSpace(nombre))
            throw new KeyNotFoundException("La columna 'Nombre' es requerida y no puede estar vacia.");

        var activo = LeerBoolConDefault(fila, "Activo", defaultValue: true);

        return new CategoriaGastoImportDto
        {
            _Fila = fila.NumeroFila,
            Nombre = nombre.Trim(),
            Activo = activo
        };
    }
}
