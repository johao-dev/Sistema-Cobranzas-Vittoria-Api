using Cobranzas_Vittoria.Application.Importacion.Dtos;
using Cobranzas_Vittoria.Application.Importacion.Parsers;
using Cobranzas_Vittoria.Application.Importacion.Persistence;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Domain.Importacion;

namespace Cobranzas_Vittoria.Application.Importacion.Processors;

/// <summary>
/// Processor de importacion masiva para <c>maestra.Material</c>.
///
/// Encabezados requeridos: <c>IdEspecialidad</c>, <c>Descripcion</c>, <c>UnidadMedida</c>.
/// Opcionales: <c>Codigo</c>, <c>StockMinimo</c>, <c>Activo</c>, <c>IdUnidadMedida</c>, <c>CodigoProveedor</c>.
///
/// Reglas de mapeo:
///   - <c>IdEspecialidad</c>: requerido, debe ser entero. La validacion de
///     existencia se hace en el SP (no hay forma rapida de chequear FKs en API).
///   - <c>Codigo</c>: opcional. Si llega vacio, se envia NULL al SP y este
///     autogenera <c>MAT-####</c>.
///   - <c>Descripcion</c>: requerido (no vacio).
///   - <c>UnidadMedida</c>: requerido, string libre.
///   - <c>StockMinimo</c>: opcional, default 0.
///   - <c>IdUnidadMedida</c>: opcional, entero o vacio.
///   - <c>CodigoProveedor</c>: opcional, string libre.
/// </summary>
public class MaterialImportProcessor : ImportProcessorBase<MaterialImportDto>
{
    public const string ModuloNombre = "material";

    public MaterialImportProcessor(
        FileParserResolver parserResolver,
        IImportRepository repository,
        IDbConnectionFactory connectionFactory)
        : base(parserResolver, repository, connectionFactory) { }

    public override string Modulo => ModuloNombre;

    protected override string SpName => "maestra.usp_Material_CargaMasiva";
    protected override string TvpTypeName => "maestra.TVP_Material";

    protected override string[] EncabezadosRequeridos => new[]
    {
        "IdEspecialidad", "Descripcion", "UnidadMedida"
    };

    protected override MaterialImportDto MapearFila(SpreadsheetRow fila)
    {
        // IdEspecialidad: requerido y debe ser entero. Si la columna no existe
        // o esta vacia, GetInt32 lanza KeyNotFoundException.
        var idEspecialidad = fila.GetInt32("IdEspecialidad");

        // Descripcion: requerido, no vacio.
        var descripcion = fila.GetString("Descripcion");
        if (string.IsNullOrWhiteSpace(descripcion))
            throw new KeyNotFoundException("La columna 'Descripcion' es requerida y no puede estar vacia.");

        // UnidadMedida: requerido, no vacio.
        var unidadMedida = fila.GetString("UnidadMedida");
        if (string.IsNullOrWhiteSpace(unidadMedida))
            throw new KeyNotFoundException("La columna 'UnidadMedida' es requerida y no puede estar vacia.");

        // Codigo: opcional, se envia tal cual (incluso vacio) para que el SP
        // detecte NULL con NULLIF y autogenere.
        var codigo = fila.GetString("Codigo");

        // StockMinimo: opcional, default 0. Si la columna existe, intentamos
        // parsear; si no existe, default 0.
        decimal stockMinimo = 0m;
        if (fila.ContieneColumna("StockMinimo") && fila.TryGetString("StockMinimo", out var stockStr) && stockStr is not null)
        {
            if (!fila.TryGetDecimal("StockMinimo", out stockMinimo))
                throw new FormatException($"La columna 'StockMinimo' no es un decimal valido: '{stockStr}'.");
        }

        // Activo: opcional, default true.
        bool activo = true;
        if (fila.ContieneColumna("Activo") && fila.TryGetString("Activo", out var activoStr) && activoStr is not null)
        {
            if (!fila.TryGetBool("Activo", out activo))
                throw new FormatException($"La columna 'Activo' contiene un valor booleano invalido: '{activoStr}'.");
        }

        // IdUnidadMedida: opcional, entero o vacio.
        int? idUnidadMedida = null;
        if (fila.ContieneColumna("IdUnidadMedida") && fila.TryGetString("IdUnidadMedida", out var idUMStr) && idUMStr is not null)
        {
            if (!fila.TryGetInt32("IdUnidadMedida", out var idUM))
                throw new FormatException($"La columna 'IdUnidadMedida' no es un entero valido: '{idUMStr}'.");
            idUnidadMedida = idUM;
        }

        // CodigoProveedor: opcional, string libre.
        var codigoProveedor = fila.GetString("CodigoProveedor");

        return new MaterialImportDto
        {
            _Fila = fila.NumeroFila,
            IdEspecialidad = idEspecialidad,
            Codigo = codigo?.Trim(),
            Descripcion = descripcion.Trim(),
            UnidadMedida = unidadMedida.Trim(),
            StockMinimo = stockMinimo,
            Activo = activo,
            IdUnidadMedida = idUnidadMedida,
            CodigoProveedor = codigoProveedor?.Trim()
        };
    }
}
