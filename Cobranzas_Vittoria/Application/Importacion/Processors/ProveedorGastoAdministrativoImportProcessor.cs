using Cobranzas_Vittoria.Application.Importacion.Dtos;
using Cobranzas_Vittoria.Application.Importacion.Parsers;
using Cobranzas_Vittoria.Application.Importacion.Persistence;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Domain.Importacion;
using Microsoft.Extensions.Logging;

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
        IDbConnectionFactory connectionFactory,
        ILogger<ProveedorGastoAdministrativoImportProcessor> logger)
        : base(parserResolver, repository, connectionFactory, logger) { }

    public override string Modulo => ModuloNombre;

    protected override string SpName => "maestra.usp_ProveedorGastoAdministrativo_CargaMasiva";
    protected override string TvpTypeName => "maestra.TVP_ProveedorGastoAdministrativo";

    protected override string[] EncabezadosRequeridos => new[] { "RazonSocial" };

    internal override ProveedorGastoAdministrativoImportDto MapearFila(SpreadsheetRow fila)
    {
        var razonSocial = fila.GetString("RazonSocial");
        if (string.IsNullOrWhiteSpace(razonSocial))
            throw new KeyNotFoundException("La columna 'RazonSocial' es requerida y no puede estar vacia.");

        var idCategoriaGasto = LeerIntNullable(fila, "IdCategoriaGasto");
        var activo = LeerBoolConDefault(fila, "Activo", defaultValue: true);

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
