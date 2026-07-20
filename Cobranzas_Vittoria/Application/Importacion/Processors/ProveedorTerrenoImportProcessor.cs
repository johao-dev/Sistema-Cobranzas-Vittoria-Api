using Cobranzas_Vittoria.Application.Importacion.Dtos;
using Cobranzas_Vittoria.Application.Importacion.Parsers;
using Cobranzas_Vittoria.Application.Importacion.Persistence;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Domain.Importacion;
using Microsoft.Extensions.Logging;

namespace Cobranzas_Vittoria.Application.Importacion.Processors;

/// <summary>
/// Processor de importacion masiva para <c>maestra.ProveedorTerreno</c>.
///
/// Encabezados requeridos: <c>RazonSocial</c>.
/// Opcionales: <c>Ruc</c>, datos de contacto, <c>Activo</c>.
/// </summary>
public class ProveedorTerrenoImportProcessor : ImportProcessorBase<ProveedorTerrenoImportDto>
{
    public const string ModuloNombre = "proveedor-terreno";

    public ProveedorTerrenoImportProcessor(
        FileParserResolver parserResolver,
        IImportRepository repository,
        IDbConnectionFactory connectionFactory,
        ILogger<ProveedorTerrenoImportProcessor> logger)
        : base(parserResolver, repository, connectionFactory, logger) { }

    public override string Modulo => ModuloNombre;

    protected override string SpName => "maestra.usp_ProveedorTerreno_CargaMasiva";
    protected override string TvpTypeName => "maestra.TVP_ProveedorTerreno";

    protected override string[] EncabezadosRequeridos => new[] { "RazonSocial" };

    internal override ProveedorTerrenoImportDto MapearFila(SpreadsheetRow fila)
    {
        var razonSocial = fila.GetString("RazonSocial");
        if (string.IsNullOrWhiteSpace(razonSocial))
            throw new KeyNotFoundException("La columna 'RazonSocial' es requerida y no puede estar vacia.");

        var activo = LeerBoolConDefault(fila, "Activo", defaultValue: true);

        return new ProveedorTerrenoImportDto
        {
            _Fila = fila.NumeroFila,
            RazonSocial = razonSocial.Trim(),
            Ruc = fila.GetString("Ruc")?.Trim(),
            Contacto = fila.GetString("Contacto")?.Trim(),
            Telefono = fila.GetString("Telefono")?.Trim(),
            Correo = fila.GetString("Correo")?.Trim(),
            Activo = activo
        };
    }
}
