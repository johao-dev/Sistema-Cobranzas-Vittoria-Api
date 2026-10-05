using System.Data;
using Cobranzas_Vittoria.Application.Importacion.Processors;
using Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Importacion.Dtos;
using Cobranzas_Vittoria.Application.Importacion;
using Cobranzas_Vittoria.Application.Importacion.Excepciones;
using Cobranzas_Vittoria.Application.Importacion.Parsers;
using Cobranzas_Vittoria.Application.Importacion.Persistence;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Domain.Importacion;
using Dapper;

using static Cobranzas_Vittoria.Application.Importacion.Processors.LecturaCatalogo;

namespace Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Importacion;

/// <summary>
/// Importación masiva del catálogo de partidas presupuestales (todo o nada).
///
/// No se registra como <see cref="IImportProcessor"/> a propósito: el endpoint
/// genérico /api/import/{modulo} no exige autenticación. Se invoca desde
/// CatalogoPartidaController (vía IImportadorMaestros), protegido por el permiso de crear partidas.
///
/// Columnas: Codigo, Nombre, Tipo (requeridas); CodigoPadre, Seccion,
/// Descripcion (opcionales). Tipo y Seccion aceptan el código o el nombre.
/// Las hijas pueden venir antes que sus padres: el SP resuelve el orden.
/// </summary>
public sealed class CatalogoPartidaImportProcessor
    : ImportProcessorBase<CatalogoPartidaImportDto, CatalogoPartidaImportTvpDto>
{
    public const string ModuloNombre = "partida";

    public static readonly string[] Columnas =
        { "Codigo", "Nombre", "Tipo", "CodigoPadre", "Seccion", "Descripcion" };

    public CatalogoPartidaImportProcessor(
        FileParserResolver parserResolver,
        IImportRepository repository,
        IDbConnectionFactory connectionFactory,
        ILogger<CatalogoPartidaImportProcessor> logger)
        : base(parserResolver, repository, connectionFactory, logger) { }

    public override string Modulo => ModuloNombre;

    protected override string SpName => "ControlPresupuestario.usp_CatalogoPartida_CargaMasiva";
    protected override string TvpTypeName => "ControlPresupuestario.TVP_CatalogoPartida";

    protected override string[] EncabezadosRequeridos => new[] { "Codigo", "Nombre", "Tipo" };

    internal override CatalogoPartidaImportDto MapearFila(SpreadsheetRow fila)
    {
        var errores = new List<DetalleErrorFila>();
        var codigo = Leer(fila, "Codigo", 50, requerido: true, errores);
        var nombre = Leer(fila, "Nombre", 200, requerido: true, errores);
        var tipo = Leer(fila, "Tipo", 100, requerido: true, errores);
        var padre = Leer(fila, "CodigoPadre", 50, requerido: false, errores);
        var seccion = Leer(fila, "Seccion", 100, requerido: false, errores);
        var descripcion = Leer(fila, "Descripcion", 500, requerido: false, errores);

        var dto = new CatalogoPartidaImportDto
        {
            _Fila = fila.NumeroFila,
            Codigo = codigo ?? string.Empty,
            Nombre = nombre ?? string.Empty,
            Tipo = tipo ?? string.Empty,
            CodigoPadre = padre,
            Seccion = seccion,
            Descripcion = descripcion
        };
        dto.ErroresFormato.AddRange(errores);
        return dto;
    }

    protected override async Task<IReadOnlyList<CatalogoPartidaImportTvpDto>> OnConstruirTvpAsync(
        IReadOnlyList<CatalogoPartidaImportDto> archivos,
        IDbConnection cn,
        IDbTransaction tx,
        CancellationToken ct)
    {
        var tipos = (await cn.QueryAsync<ReferenciaCatalogo>(new CommandDefinition(
            "SELECT IdTipoPartida AS Id, Codigo, Nombre, Activo FROM ControlPresupuestario.TipoPartida",
            transaction: tx, cancellationToken: ct))).AsList();
        var secciones = (await cn.QueryAsync<ReferenciaCatalogo>(new CommandDefinition(
            "SELECT IdSeccionGasto AS Id, Codigo, Nombre, Activo FROM ControlPresupuestario.SeccionGasto",
            transaction: tx, cancellationToken: ct))).AsList();
        var existentes = (await cn.QueryAsync<PartidaExistente>(new CommandDefinition("""
            SELECT p.Codigo, p.Activo, p.IdSeccionGasto,
                CONVERT(BIT, CASE WHEN EXISTS (SELECT 1 FROM ControlPresupuestario.PresupuestoDetalle d
                    WHERE d.IdCatalogoPartida = p.IdCatalogoPartida) THEN 1 ELSE 0 END) AS TieneMontos
            FROM ControlPresupuestario.CatalogoPartida p
            """, transaction: tx, cancellationToken: ct)))
            .ToDictionary(p => p.Codigo, StringComparer.OrdinalIgnoreCase);

        var errores = archivos.SelectMany(f => f.ErroresFormato).ToList();
        var porCodigo = new Dictionary<string, CatalogoPartidaImportDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var fila in archivos.Where(f => f.Codigo.Length > 0))
        {
            if (!porCodigo.TryAdd(fila.Codigo, fila))
                errores.Add(Error(fila, "Codigo", CodigosError.Sp.ValorDuplicadoEnArchivo,
                    $"El código {fila.Codigo} ya aparece en la fila {porCodigo[fila.Codigo]._Fila}."));
        }
        var codigosPadre = archivos
            .Where(f => f.CodigoPadre is not null)
            .Select(f => f.CodigoPadre!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var tvps = new List<CatalogoPartidaImportTvpDto>(archivos.Count);
        foreach (var fila in archivos)
        {
            if (existentes.ContainsKey(fila.Codigo))
                errores.Add(Error(fila, "Codigo", CodigosError.Sp.ValorYaExisteEnBd,
                    $"El código {fila.Codigo} ya existe en el catálogo."));

            // Un tipo vacío ya se informó como error de formato.
            var tipo = fila.Tipo.Length > 0 ? Resolver(tipos, fila.Tipo) : null;
            if (tipo is null && fila.Tipo.Length > 0)
                errores.Add(Error(fila, "Tipo", CodigosError.Sp.FkNoExiste,
                    $"El tipo '{fila.Tipo}' no existe. Use uno de: {Listar(tipos)}."));
            else if (tipo is not null && !tipo.Activo)
                errores.Add(Error(fila, "Tipo", CodigosError.Fila.ReglaNegocio,
                    $"El tipo '{fila.Tipo}' está inactivo."));

            ReferenciaCatalogo? seccion = null;
            if (fila.Seccion is not null)
            {
                seccion = Resolver(secciones, fila.Seccion);
                if (seccion is null)
                    errores.Add(Error(fila, "Seccion", CodigosError.Sp.FkNoExiste,
                        $"La sección '{fila.Seccion}' no existe. Use una de: {Listar(secciones)}."));
                else if (!seccion.Activo)
                    errores.Add(Error(fila, "Seccion", CodigosError.Fila.ReglaNegocio,
                        $"La sección '{fila.Seccion}' está inactiva."));
                else if (codigosPadre.Contains(fila.Codigo))
                    errores.Add(Error(fila, "Seccion", CodigosError.Fila.ReglaNegocio,
                        $"La partida {fila.Codigo} tiene hijas en el archivo; solo una partida sin hijas puede tener sección."));
            }

            if (fila.CodigoPadre is not null)
                ValidarPadre(fila, porCodigo, existentes, errores);

            tvps.Add(new CatalogoPartidaImportTvpDto
            {
                Codigo = fila.Codigo,
                Nombre = fila.Nombre,
                IdTipoPartida = tipo?.Id ?? 0,
                CodigoPadre = fila.CodigoPadre,
                IdSeccionGasto = seccion?.Id,
                Descripcion = fila.Descripcion,
                _Fila = fila._Fila
            });
        }

        DetectarCiclos(archivos, porCodigo, errores);

        if (errores.Count > 0)
        {
            var ordenados = errores.OrderBy(e => e.Fila).ThenBy(e => e.Campo).ToList();
            throw new DatosInvalidosException(
                $"El archivo contiene {ordenados.Count} error(es). No se importó ninguna partida.", ordenados);
        }

        return tvps;
    }

    private static void ValidarPadre(CatalogoPartidaImportDto fila,
        IReadOnlyDictionary<string, CatalogoPartidaImportDto> porCodigo,
        IReadOnlyDictionary<string, PartidaExistente> existentes,
        List<DetalleErrorFila> errores)
    {
        var padre = fila.CodigoPadre!;
        if (string.Equals(padre, fila.Codigo, StringComparison.OrdinalIgnoreCase))
        {
            errores.Add(Error(fila, "CodigoPadre", CodigosError.Fila.ReglaNegocio,
                "Una partida no puede ser su propio padre."));
            return;
        }
        if (existentes.TryGetValue(padre, out var enBd))
        {
            var motivo = !enBd.Activo ? "está inactiva"
                : enBd.IdSeccionGasto is not null ? "tiene sección de gasto y no puede tener hijas"
                : enBd.TieneMontos ? "ya tiene montos presupuestados y no puede convertirse en agrupadora"
                : null;
            if (motivo is not null)
                errores.Add(Error(fila, "CodigoPadre", CodigosError.Fila.ReglaNegocio,
                    $"La partida padre {padre} {motivo}."));
            return;
        }
        if (!porCodigo.ContainsKey(padre))
            errores.Add(Error(fila, "CodigoPadre", CodigosError.Sp.FkNoExiste,
                $"La partida padre {padre} no existe en el catálogo ni en el archivo."));
    }

    /// <summary>Marca las filas cuya cadena de padres dentro del archivo vuelve sobre sí misma.</summary>
    private static void DetectarCiclos(IReadOnlyList<CatalogoPartidaImportDto> archivos,
        IReadOnlyDictionary<string, CatalogoPartidaImportDto> porCodigo,
        List<DetalleErrorFila> errores)
    {
        foreach (var fila in archivos)
        {
            var visitados = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { fila.Codigo };
            var actual = fila.CodigoPadre;
            while (actual is not null && porCodigo.TryGetValue(actual, out var siguiente))
            {
                if (!visitados.Add(actual))
                {
                    errores.Add(Error(fila, "CodigoPadre", CodigosError.Fila.ReglaNegocio,
                        $"La partida {fila.Codigo} forma un ciclo con sus partidas padre."));
                    break;
                }
                actual = siguiente.CodigoPadre;
            }
        }
    }

    private static DetalleErrorFila Error(CatalogoPartidaImportDto fila, string campo, string codigo, string mensaje)
        => new(fila._Fila, campo, codigo, mensaje);

    private sealed class PartidaExistente
    {
        public string Codigo { get; set; } = string.Empty;
        public bool Activo { get; set; }
        public int? IdSeccionGasto { get; set; }
        public bool TieneMontos { get; set; }
    }
}
