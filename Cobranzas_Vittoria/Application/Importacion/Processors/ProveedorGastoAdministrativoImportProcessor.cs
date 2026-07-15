using Cobranzas_Vittoria.Application.Importacion.Dtos;
using Cobranzas_Vittoria.Application.Importacion.Parsers;
using Cobranzas_Vittoria.Application.Importacion.Persistence;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Domain.Importacion;

namespace Cobranzas_Vittoria.Application.Importacion.Processors;

/// <summary>
/// Processor de importacion masiva para <c>maestra.ProveedorGastoAdministrativo</c>.
///
/// Encabezados requeridos: <c>RazonSocial</c>.
/// Opcionales: <c>Ruc</c>, datos de contacto, <c>IdCategoriaGasto</c>, <c>Activo</c>.
///
/// Reglas de mapeo:
///   - <c>RazonSocial</c>: requerido, no vacio (unicidad validada en SP).
///   - <c>Ruc</c>: opcional, validacion de unicidad en SP solo si se proporciona.
///   - <c>IdCategoriaGasto</c>: opcional, entero; FK validada en SP.
/// </summary>
public class ProveedorGastoAdministrativoImportProcessor : ImportProcessorBase<ProveedorGastoAdministrativoImportDto>
{
    public const string ModuloNombre = "proveedor-gasto";

    public ProveedorGastoAdministrativoImportProcessor(
        FileParserResolver parserResolver,
        IImportRepository repository,
        IDbConnectionFactory connectionFactory)
        : base(parserResolver, repository, connectionFactory) { }

    public override string Modulo => ModuloNombre;

    protected override string SpName => "maestra.usp_ProveedorGastoAdministrativo_CargaMasiva";
    protected override string TvpTypeName => "maestra.TVP_ProveedorGastoAdministrativo";

    protected override string[] EncabezadosRequeridos => new[] { "RazonSocial" };

    protected override ProveedorGastoAdministrativoImportDto MapearFila(SpreadsheetRow fila)
    {
        var razonSocial = fila.GetString("RazonSocial");
        if (string.IsNullOrWhiteSpace(razonSocial))
            throw new KeyNotFoundException("La columna 'RazonSocial' es requerida y no puede estar vacia.");

        int? idCategoriaGasto = null;
        if (fila.ContieneColumna("IdCategoriaGasto") && fila.TryGetString("IdCategoriaGasto", out var idCatStr) && idCatStr is not null)
        {
            if (!fila.TryGetInt32("IdCategoriaGasto", out var idCat))
                throw new FormatException($"La columna 'IdCategoriaGasto' no es un entero valido: '{idCatStr}'.");
            idCategoriaGasto = idCat;
        }

        bool activo = true;
        if (fila.ContieneColumna("Activo") && fila.TryGetString("Activo", out var activoStr) && activoStr is not null)
        {
            if (!fila.TryGetBool("Activo", out activo))
                throw new FormatException($"La columna 'Activo' contiene un valor booleano invalido: '{activoStr}'.");
        }

        return new ProveedorGastoAdministrativoImportDto
        {
            _Fila = fila.NumeroFila,
            RazonSocial = razonSocial.Trim(),
            Ruc = fila.GetString("Ruc")?.Trim(),
            Contacto = fila.GetString("Contacto")?.Trim(),
            Telefono = fila.GetString("Telefono")?.Trim(),
            Correo = fila.GetString("Correo")?.Trim(),
            Activo = activo,
            IdCategoriaGasto = idCategoriaGasto
        };
    }
}
