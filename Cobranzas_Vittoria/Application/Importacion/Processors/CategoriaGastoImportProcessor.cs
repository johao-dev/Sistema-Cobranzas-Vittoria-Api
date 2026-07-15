using Cobranzas_Vittoria.Application.Importacion.Dtos;
using Cobranzas_Vittoria.Application.Importacion.Parsers;
using Cobranzas_Vittoria.Application.Importacion.Persistence;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Domain.Importacion;

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
        IDbConnectionFactory connectionFactory)
        : base(parserResolver, repository, connectionFactory) { }

    public override string Modulo => ModuloNombre;

    protected override string SpName => "maestra.usp_CategoriaGasto_CargaMasiva";
    protected override string TvpTypeName => "maestra.TVP_CategoriaGasto";

    protected override string[] EncabezadosRequeridos => new[] { "Nombre" };

    protected override CategoriaGastoImportDto MapearFila(SpreadsheetRow fila)
    {
        var nombre = fila.GetString("Nombre");
        if (string.IsNullOrWhiteSpace(nombre))
            throw new KeyNotFoundException("La columna 'Nombre' es requerida y no puede estar vacia.");

        bool activo = true;
        if (fila.ContieneColumna("Activo") && fila.TryGetString("Activo", out var activoStr) && activoStr is not null)
        {
            if (!fila.TryGetBool("Activo", out activo))
                throw new FormatException($"La columna 'Activo' contiene un valor booleano invalido: '{activoStr}'.");
        }

        return new CategoriaGastoImportDto
        {
            _Fila = fila.NumeroFila,
            Nombre = nombre.Trim(),
            Activo = activo
        };
    }
}
