using System.Data;
using Cobranzas_Vittoria.Application.Importacion.Dtos;
using Cobranzas_Vittoria.Application.Importacion.Excepciones;
using Cobranzas_Vittoria.Application.Importacion.Parsers;
using Cobranzas_Vittoria.Application.Importacion.Persistence;
using Cobranzas_Vittoria.Application.Importacion.Services;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Domain.Importacion;
using Dapper;

namespace Cobranzas_Vittoria.Application.Importacion.Processors;

/// <summary>
/// Processor de importacion masiva para <c>maestra.Material</c>.
///
/// Encabezados requeridos (plantilla amigable de 4 columnas):
///   - <c>Especialidad</c>: nombre de la Especialidad. Se resuelve contra
///     <c>maestra.Especialidad</c> (case/accent-insensitive); si no existe,
///     se CREA en la misma transaccion (atomicidad).
///   - <c>Nombre</c>:       descripcion del material (se mapea a Descripcion).
///   - <c>UnidadMedida</c>: nombre de la unidad. Se resuelve contra
///     <c>maestra.UnidadMedida</c>; si no existe, se crea con codigo
///     autogenerado "UM-&lt;SIGLA&gt;-####".
///   - <c>Codigo</c>:       codigo del material, REQUERIDO (no se autogenera).
///
/// <para>
/// <b>Diferencias con v1:</b>
/// <list type="bullet">
///   <item>El archivo no trae IDs, solo nombres. La conversion nombre -&gt; ID
///         ocurre en este processor via <see cref="ResolvedorEntidadesService"/>,
///         DENTRO de la transaccion que abrio la base, antes de invocar el SP.</item>
///   <item>El SP objetivo es <c>maestra.usp_Material_CargaMasiva_v2</c> (con
///         TVP <c>maestra.TVP_Material_v2</c>).</item>
///   <item>Codigo es obligatorio: si viene vacio, la fila se rechaza con
///         CAMPO_REQUERIDO.</item>
/// </list>
/// </para>
/// </summary>
public class MaterialImportProcessor : ImportProcessorBase<MaterialImportDto, MaterialImportTvpV3Dto>
{
    public const string ModuloNombre = "material";

    private readonly ResolvedorEntidadesService _resolvedor;

    public MaterialImportProcessor(
        FileParserResolver parserResolver,
        IImportRepository repository,
        IDbConnectionFactory connectionFactory,
        ResolvedorEntidadesService resolvedor,
        ILogger<MaterialImportProcessor> logger)
        : base(parserResolver, repository, connectionFactory, logger)
    {
        _resolvedor = resolvedor ?? throw new ArgumentNullException(nameof(resolvedor));
    }

    public override string Modulo => ModuloNombre;

    // v3 = v2 + partida por defecto (columna opcional "Partida" con el código de la partida).
    protected override string SpName => "maestra.usp_Material_CargaMasiva_v3";
    protected override string TvpTypeName => "maestra.TVP_Material_v3";

    protected override string[] EncabezadosRequeridos => new[]
    {
        "Especialidad", "Nombre", "UnidadMedida", "Codigo"
    };

    internal override MaterialImportDto MapearFila(SpreadsheetRow fila)
    {
        // Especialidad: requerido, no vacio. Se valida vacio aqui (no en el
        // SP) porque la resolucion del catalogo es responsabilidad del processor.
        var especialidad = fila.GetString("Especialidad");
        if (string.IsNullOrWhiteSpace(especialidad))
            throw new KeyNotFoundException("La columna 'Especialidad' es requerida y no puede estar vacia.");

        // Nombre (Descripcion): requerido, no vacio.
        var nombre = fila.GetString("Nombre");
        if (string.IsNullOrWhiteSpace(nombre))
            throw new KeyNotFoundException("La columna 'Nombre' es requerida y no puede estar vacia.");

        // UnidadMedida: requerido, no vacio.
        var unidadMedida = fila.GetString("UnidadMedida");
        if (string.IsNullOrWhiteSpace(unidadMedida))
            throw new KeyNotFoundException("La columna 'UnidadMedida' es requerida y no puede estar vacia.");

        // Codigo: requerido, no vacio. En v2 NO se autogenera.
        var codigo = fila.GetString("Codigo");
        if (string.IsNullOrWhiteSpace(codigo))
            throw new KeyNotFoundException("La columna 'Codigo' es requerida y no puede estar vacia.");

        // Partida: opcional. Código de una partida hoja del catálogo presupuestal.
        var partida = fila.ContieneColumna("Partida") ? fila.GetString("Partida")?.Trim() : null;

        return new MaterialImportDto
        {
            _Fila = fila.NumeroFila,
            Especialidad = especialidad.Trim(),
            Nombre = nombre.Trim(),
            UnidadMedida = unidadMedida.Trim(),
            Codigo = codigo.Trim(),
            Partida = string.IsNullOrEmpty(partida) ? null : partida
        };
    }

    /// <summary>
    /// Construye los DTOs de TVP a partir de los DTOs de archivo. Aqui ocurre
    /// la RESOLUCION de catalogos (Especialidad, UnidadMedida) DENTRO de la
    /// transaccion que abrio la base. Si una creacion choca con UNIQUE por
    /// concurrencia, el <see cref="ResolvedorEntidadesService"/> reintenta.
    /// </summary>
    protected override async Task<IReadOnlyList<MaterialImportTvpV3Dto>> OnConstruirTvpAsync(
        IReadOnlyList<MaterialImportDto> archivos,
        IDbConnection cn,
        IDbTransaction tx,
        CancellationToken ct)
    {
        var partidas = await ResolverPartidasAsync(archivos, cn, tx, ct);

        var tvps = new List<MaterialImportTvpV3Dto>(archivos.Count);
        foreach (var a in archivos)
        {
            var idEspecialidad = await _resolvedor.ResolverIdEspecialidadAsync(
                a.Especialidad, cn, tx, ct);
            var idUnidadMedida = await _resolvedor.ResolverIdUnidadMedidaAsync(
                a.UnidadMedida, cn, tx, ct);

            tvps.Add(new MaterialImportTvpV3Dto
            {
                _Fila = a._Fila,
                IdEspecialidad = idEspecialidad,
                Codigo = a.Codigo,
                Descripcion = a.Nombre,
                IdUnidadMedida = idUnidadMedida,
                UnidadMedida = a.UnidadMedida,
                IdCatalogoPartida = a.Partida is null ? null : partidas[a.Partida]
            });
        }
        return tvps;
    }

    /// <summary>
    /// Resuelve los códigos de la columna Partida. Cada código debe existir, estar
    /// activo y no tener partidas hijas; si alguno falla se informan todas las filas
    /// con error y no se importa nada.
    /// </summary>
    private static async Task<IReadOnlyDictionary<string, int>> ResolverPartidasAsync(
        IReadOnlyList<MaterialImportDto> archivos, IDbConnection cn, IDbTransaction tx, CancellationToken ct)
    {
        var resueltas = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (!archivos.Any(a => a.Partida is not null)) return resueltas;

        var catalogo = (await cn.QueryAsync<(int Id, string Codigo, bool Activo, bool TieneHijas)>(
            new CommandDefinition("""
                SELECT p.IdCatalogoPartida, p.Codigo, p.Activo,
                    CONVERT(BIT, CASE WHEN EXISTS (SELECT 1 FROM ControlPresupuestario.CatalogoPartida h
                        WHERE h.IdPartidaPadre = p.IdCatalogoPartida) THEN 1 ELSE 0 END)
                FROM ControlPresupuestario.CatalogoPartida p
                """, transaction: tx, cancellationToken: ct)))
            .ToDictionary(p => p.Codigo, StringComparer.OrdinalIgnoreCase);

        var errores = new List<DetalleErrorFila>();
        foreach (var a in archivos.Where(a => a.Partida is not null))
        {
            var motivo = !catalogo.TryGetValue(a.Partida!, out var p) ? "no existe en el catálogo"
                : !p.Activo ? "está inactiva"
                : p.TieneHijas ? "tiene partidas hijas; usa una partida sin hijas"
                : null;
            if (motivo is null) resueltas[a.Partida!] = p.Id;
            else errores.Add(new DetalleErrorFila(a._Fila, "Partida", CodigosError.Fila.ReglaNegocio,
                $"La partida {a.Partida} {motivo}."));
        }

        if (errores.Count > 0)
            throw new DatosInvalidosException(
                $"El archivo contiene {errores.Count} fila(s) con una partida inválida. No se realizo ninguna insercion.",
                errores);
        return resueltas;
    }
}
