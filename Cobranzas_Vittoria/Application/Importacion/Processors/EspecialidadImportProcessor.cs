using Cobranzas_Vittoria.Application.Importacion.Dtos;
using Cobranzas_Vittoria.Application.Importacion.Parsers;
using Cobranzas_Vittoria.Application.Importacion.Persistence;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Domain.Importacion;
using Microsoft.Extensions.Logging;

namespace Cobranzas_Vittoria.Application.Importacion.Processors;

/// <summary>
/// Processor de importacion masiva para <c>maestra.Especialidad</c>.
///
/// Encabezados requeridos: <c>Nombre</c>.
/// Opcionales: <c>Descripcion</c>, <c>Activo</c> (default true).
/// </summary>
public class EspecialidadImportProcessor : ImportProcessorBase<EspecialidadImportDto, EspecialidadImportDto>
{
    public const string ModuloNombre = "especialidad";

    public EspecialidadImportProcessor(
        FileParserResolver parserResolver,
        IImportRepository repository,
        IDbConnectionFactory connectionFactory,
        ILogger<EspecialidadImportProcessor> logger)
        : base(parserResolver, repository, connectionFactory, logger) { }

    public override string Modulo => ModuloNombre;

    protected override string SpName => "maestra.usp_Especialidad_CargaMasiva";
    protected override string TvpTypeName => "maestra.TVP_Especialidad";

    protected override string[] EncabezadosRequeridos => new[] { "Nombre" };

    internal override EspecialidadImportDto MapearFila(SpreadsheetRow fila)
    {
        var nombre = fila.GetString("Nombre");
        if (string.IsNullOrWhiteSpace(nombre))
            throw new KeyNotFoundException("La columna 'Nombre' es requerida y no puede estar vacia.");

        var descripcion = fila.GetString("Descripcion"); // opcional, null si vacia o no existe

        var activo = LeerBoolConDefault(fila, "Activo", defaultValue: true);

        return new EspecialidadImportDto
        {
            _Fila = fila.NumeroFila,
            Nombre = nombre.Trim(),
            Descripcion = descripcion?.Trim(),
            Activo = activo
        };
    }
}
