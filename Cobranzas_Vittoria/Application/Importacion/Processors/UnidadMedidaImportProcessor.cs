using Cobranzas_Vittoria.Application.Importacion.Dtos;
using Cobranzas_Vittoria.Application.Importacion.Parsers;
using Cobranzas_Vittoria.Application.Importacion.Persistence;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Domain.Importacion;

namespace Cobranzas_Vittoria.Application.Importacion.Processors;

/// <summary>
/// Processor de importacion masiva para <c>maestra.UnidadMedida</c>.
///
/// Encabezados requeridos (case-insensitive): <c>Codigo</c>, <c>Nombre</c>.
/// Opcionales: <c>Activo</c> (default true).
///
/// Reglas de mapeo:
///   - <c>Codigo</c>: requerido, se trimea. Si llega vacio o solo espacios,
///     <see cref="SpreadsheetRow.GetString"/> devuelve null y el accesor tipado
///     <c>GetString</c> retorna null; el processor lo traduce a fila con error.
///   - <c>Nombre</c>: requerido, se trimea. Mismo manejo que Codigo.
///   - <c>Activo</c>: opcional. Si la columna no existe o esta vacia, default true.
/// </summary>
public class UnidadMedidaImportProcessor : ImportProcessorBase<UnidadMedidaImportDto>
{
    /// <summary>Modulo expuesto en la URL: <c>POST /api/import/unidad-medida</c>.</summary>
    public const string ModuloNombre = "unidad-medida";

    public UnidadMedidaImportProcessor(
        FileParserResolver parserResolver,
        IImportRepository repository,
        IDbConnectionFactory connectionFactory)
        : base(parserResolver, repository, connectionFactory) { }

    public override string Modulo => ModuloNombre;

    protected override string SpName => "maestra.usp_UnidadMedida_CargaMasiva";
    protected override string TvpTypeName => "maestra.TVP_UnidadMedida";

    protected override string[] EncabezadosRequeridos => new[] { "Codigo", "Nombre" };

    protected override UnidadMedidaImportDto MapearFila(SpreadsheetRow fila)
    {
        // Codigo: trim y validacion explicita. Si llega vacio, lanzamos para
        // que la base lo reporte como CAMPO_REQUERIDO con la fila correcta.
        var codigo = fila.GetString("Codigo");
        if (string.IsNullOrWhiteSpace(codigo))
            throw new KeyNotFoundException("La columna 'Codigo' es requerida y no puede estar vacia.");

        // Nombre: idem Codigo.
        var nombre = fila.GetString("Nombre");
        if (string.IsNullOrWhiteSpace(nombre))
            throw new KeyNotFoundException("La columna 'Nombre' es requerida y no puede estar vacia.");

        // Activo: opcional. Si no viene o viene vacio, default true.
        // TryGetBool devuelve false si la columna no existe o el valor es vacio,
        // pero NO sabemos distinguir "no existe" de "valor invalido" sin el bool out.
        // Para mantener simple: si la columna existe con valor, lo parseamos; si no, true.
        bool activo = true;
        if (fila.ContieneColumna("Activo") && fila.TryGetString("Activo", out var activoStr) && activoStr is not null)
        {
            if (!fila.TryGetBool("Activo", out activo))
                throw new FormatException($"La columna 'Activo' contiene un valor booleano invalido: '{activoStr}'.");
        }

        return new UnidadMedidaImportDto
        {
            _Fila = fila.NumeroFila,
            Codigo = codigo.Trim(),
            Nombre = nombre.Trim(),
            Activo = activo
        };
    }
}
