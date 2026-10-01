using Cobranzas_Vittoria.ControlPresupuestario.Application.Common;
using Cobranzas_Vittoria.Application.Importacion.Parsers;
using Cobranzas_Vittoria.Application.Importacion.Validators;

namespace Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Importacion;

/// <summary>Adaptador del puerto ILectorArchivoTabular sobre el validador y los parsers compartidos (CSV/XLSX).</summary>
public sealed class LectorArchivoTabular : ILectorArchivoTabular
{
    private readonly FileValidator _validador;
    private readonly FileParserResolver _parsers;

    public LectorArchivoTabular(FileValidator validador, FileParserResolver parsers)
    {
        _validador = validador;
        _parsers = parsers;
    }

    public IReadOnlyList<FilaTabular> Leer(ArchivoTabular archivo)
    {
        var formulario = ArchivoFormulario.Crear(archivo);
        _validador.Validar(formulario);
        return _parsers.ObtenerParser(formulario).Parse(formulario)
            .Select(f => new FilaTabular(f.NumeroFila,
                f.Celdas.ToDictionary(c => c.Key, c => (string?)c.Value, StringComparer.OrdinalIgnoreCase)))
            .ToList();
    }
}
