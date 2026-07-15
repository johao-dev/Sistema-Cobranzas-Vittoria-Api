using Cobranzas_Vittoria.Application.Importacion.Dtos;
using Cobranzas_Vittoria.Application.Importacion.Parsers;
using Cobranzas_Vittoria.Application.Importacion.Persistence;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Domain.Importacion;

namespace Cobranzas_Vittoria.Application.Importacion.Processors;

/// <summary>
/// Processor de importacion masiva para <c>maestra.Proveedor</c>.
///
/// Encabezados requeridos: <c>RazonSocial</c>, <c>Ruc</c>.
/// Opcionales: resto de columnas de contacto y datos bancarios.
///
/// Reglas de mapeo:
///   - <c>RazonSocial</c>: requerido, no vacio.
///   - <c>Ruc</c>: requerido, no vacio (unicidad validada en SP).
///   - <c>TrabajamosConProveedor</c>: opcional, string libre (max 10 chars).
///   - Resto: opcionales, strings libres.
/// </summary>
public class ProveedorImportProcessor : ImportProcessorBase<ProveedorImportDto>
{
    public const string ModuloNombre = "proveedor";

    public ProveedorImportProcessor(
        FileParserResolver parserResolver,
        IImportRepository repository,
        IDbConnectionFactory connectionFactory)
        : base(parserResolver, repository, connectionFactory) { }

    public override string Modulo => ModuloNombre;

    protected override string SpName => "maestra.usp_Proveedor_CargaMasiva";
    protected override string TvpTypeName => "maestra.TVP_Proveedor";

    protected override string[] EncabezadosRequeridos => new[] { "RazonSocial", "Ruc" };

    protected override ProveedorImportDto MapearFila(SpreadsheetRow fila)
    {
        var razonSocial = fila.GetString("RazonSocial");
        if (string.IsNullOrWhiteSpace(razonSocial))
            throw new KeyNotFoundException("La columna 'RazonSocial' es requerida y no puede estar vacia.");

        var ruc = fila.GetString("Ruc");
        if (string.IsNullOrWhiteSpace(ruc))
            throw new KeyNotFoundException("La columna 'Ruc' es requerida y no puede estar vacia.");

        bool activo = true;
        if (fila.ContieneColumna("Activo") && fila.TryGetString("Activo", out var activoStr) && activoStr is not null)
        {
            if (!fila.TryGetBool("Activo", out activo))
                throw new FormatException($"La columna 'Activo' contiene un valor booleano invalido: '{activoStr}'.");
        }

        return new ProveedorImportDto
        {
            _Fila = fila.NumeroFila,
            RazonSocial = razonSocial.Trim(),
            Ruc = ruc.Trim(),
            Contacto = fila.GetString("Contacto")?.Trim(),
            Telefono = fila.GetString("Telefono")?.Trim(),
            Correo = fila.GetString("Correo")?.Trim(),
            Direccion = fila.GetString("Direccion")?.Trim(),
            Banco = fila.GetString("Banco")?.Trim(),
            CuentaCorriente = fila.GetString("CuentaCorriente")?.Trim(),
            CCI = fila.GetString("CCI")?.Trim(),
            CuentaDetraccion = fila.GetString("CuentaDetraccion")?.Trim(),
            DescripcionServicio = fila.GetString("DescripcionServicio")?.Trim(),
            Observacion = fila.GetString("Observacion")?.Trim(),
            TrabajamosConProveedor = fila.GetString("TrabajamosConProveedor")?.Trim(),
            Activo = activo
        };
    }
}
