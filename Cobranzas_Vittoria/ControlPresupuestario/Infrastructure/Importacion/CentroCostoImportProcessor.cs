using System.Data;
using Cobranzas_Vittoria.Application.Importacion.Processors;
using Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Importacion.Dtos;
using Cobranzas_Vittoria.Application.Importacion;
using Cobranzas_Vittoria.Application.Importacion.Excepciones;
using Cobranzas_Vittoria.Application.Importacion.Parsers;
using Cobranzas_Vittoria.Domain.Importacion;
using Cobranzas_Vittoria.Application.Importacion.Persistence;
using Cobranzas_Vittoria.Data;
using Dapper;
using static Cobranzas_Vittoria.Application.Importacion.Processors.LecturaCatalogo;

namespace Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Importacion;

/// <summary>
/// Importación masiva de centros de costo (todo o nada).
///
/// Igual que el catálogo de partidas, no se registra como <see cref="IImportProcessor"/>:
/// el endpoint genérico /api/import/{modulo} no exige autenticación. Se invoca desde
/// CentroCostoController (vía IImportadorMaestros), protegido por el permiso de crear centros de costo.
///
/// Columnas: Codigo, Nombre, Tipo (requeridas); Proyecto, Descripcion (opcionales).
/// Tipo acepta el código o el nombre; Proyecto, el nombre del proyecto. El tipo
/// PROYECTO exige un proyecto activo que no tenga ya un centro de costo, y los
/// demás tipos no admiten proyecto (mismas reglas que el alta individual).
/// </summary>
public sealed class CentroCostoImportProcessor
    : ImportProcessorBase<CentroCostoImportDto, CentroCostoImportTvpDto>
{
    public const string ModuloNombre = "centro-costo";
    private const string TipoProyecto = "PROYECTO";

    public static readonly string[] Columnas = { "Codigo", "Nombre", "Tipo", "Proyecto", "Descripcion" };

    public CentroCostoImportProcessor(
        FileParserResolver parserResolver,
        IImportRepository repository,
        IDbConnectionFactory connectionFactory,
        ILogger<CentroCostoImportProcessor> logger)
        : base(parserResolver, repository, connectionFactory, logger) { }

    public override string Modulo => ModuloNombre;

    protected override string SpName => "ControlPresupuestario.usp_CentroCosto_CargaMasiva";
    protected override string TvpTypeName => "ControlPresupuestario.TVP_CentroCosto";

    protected override string[] EncabezadosRequeridos => new[] { "Codigo", "Nombre", "Tipo" };

    internal override CentroCostoImportDto MapearFila(SpreadsheetRow fila)
    {
        var errores = new List<DetalleErrorFila>();
        var dto = new CentroCostoImportDto
        {
            _Fila = fila.NumeroFila,
            Codigo = Leer(fila, "Codigo", 30, requerido: true, errores) ?? string.Empty,
            Nombre = Leer(fila, "Nombre", 150, requerido: true, errores) ?? string.Empty,
            Tipo = Leer(fila, "Tipo", 100, requerido: true, errores) ?? string.Empty,
            Proyecto = Leer(fila, "Proyecto", 200, requerido: false, errores),
            Descripcion = Leer(fila, "Descripcion", 255, requerido: false, errores)
        };
        dto.ErroresFormato.AddRange(errores);
        return dto;
    }

    protected override async Task<IReadOnlyList<CentroCostoImportTvpDto>> OnConstruirTvpAsync(
        IReadOnlyList<CentroCostoImportDto> archivos,
        IDbConnection cn,
        IDbTransaction tx,
        CancellationToken ct)
    {
        var tipos = (await cn.QueryAsync<ReferenciaCatalogo>(new CommandDefinition(
            "SELECT IdTipoCentroCosto AS Id, Codigo, Nombre, Activo FROM ControlPresupuestario.TipoCentroCosto",
            transaction: tx, cancellationToken: ct))).AsList();
        // El proyecto se escribe por nombre; el Id numérico también se acepta como código.
        var proyectos = (await cn.QueryAsync<ReferenciaCatalogo>(new CommandDefinition(
            "SELECT IdProyecto AS Id, CONVERT(VARCHAR(20), IdProyecto) AS Codigo, NombreProyecto AS Nombre, Activo FROM maestra.Proyecto",
            transaction: tx, cancellationToken: ct))).AsList();
        var existentes = (await cn.QueryAsync<(string Codigo, int? IdProyecto)>(new CommandDefinition(
            "SELECT Codigo, IdProyecto FROM ControlPresupuestario.CentroCosto",
            transaction: tx, cancellationToken: ct))).AsList();
        var codigosEnBd = existentes.Select(e => e.Codigo).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ccPorProyecto = existentes.Where(e => e.IdProyecto is not null)
            .ToDictionary(e => e.IdProyecto!.Value, e => e.Codigo);

        var errores = archivos.SelectMany(f => f.ErroresFormato).ToList();
        var codigosArchivo = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var proyectosArchivo = new Dictionary<int, int>();
        var tvps = new List<CentroCostoImportTvpDto>(archivos.Count);

        foreach (var fila in archivos)
        {
            if (fila.Codigo.Length > 0)
            {
                if (!codigosArchivo.TryAdd(fila.Codigo, fila._Fila))
                    errores.Add(Error(fila, "Codigo", CodigosError.Sp.ValorDuplicadoEnArchivo,
                        $"El código {fila.Codigo} ya aparece en la fila {codigosArchivo[fila.Codigo]}."));
                else if (codigosEnBd.Contains(fila.Codigo))
                    errores.Add(Error(fila, "Codigo", CodigosError.Sp.ValorYaExisteEnBd,
                        $"El código {fila.Codigo} ya existe."));
            }

            // Un tipo vacío ya se informó como error de formato.
            var tipo = fila.Tipo.Length > 0 ? Resolver(tipos, fila.Tipo) : null;
            if (tipo is null && fila.Tipo.Length > 0)
                errores.Add(Error(fila, "Tipo", CodigosError.Sp.FkNoExiste,
                    $"El tipo '{fila.Tipo}' no existe. Use uno de: {Listar(tipos)}."));
            else if (tipo is not null && !tipo.Activo)
                errores.Add(Error(fila, "Tipo", CodigosError.Fila.ReglaNegocio, $"El tipo '{fila.Tipo}' está inactivo."));

            ReferenciaCatalogo? proyecto = null;
            if (fila.Proyecto is not null)
            {
                proyecto = Resolver(proyectos, fila.Proyecto);
                if (proyecto is null)
                    errores.Add(Error(fila, "Proyecto", CodigosError.Sp.FkNoExiste,
                        $"El proyecto '{fila.Proyecto}' no existe."));
                else if (!proyecto.Activo)
                    errores.Add(Error(fila, "Proyecto", CodigosError.Fila.ReglaNegocio,
                        $"El proyecto '{proyecto.Nombre}' está inactivo."));
                else if (ccPorProyecto.TryGetValue(proyecto.Id, out var ccExistente))
                    errores.Add(Error(fila, "Proyecto", CodigosError.Sp.ValorYaExisteEnBd,
                        $"El proyecto '{proyecto.Nombre}' ya tiene el centro de costo {ccExistente}."));
                else if (!proyectosArchivo.TryAdd(proyecto.Id, fila._Fila))
                    errores.Add(Error(fila, "Proyecto", CodigosError.Sp.ValorDuplicadoEnArchivo,
                        $"El proyecto '{proyecto.Nombre}' ya está asignado en la fila {proyectosArchivo[proyecto.Id]}."));
            }

            if (tipo is { Activo: true })
            {
                var esProyecto = string.Equals(tipo.Codigo, TipoProyecto, StringComparison.OrdinalIgnoreCase);
                if (esProyecto && fila.Proyecto is null)
                    errores.Add(Error(fila, "Proyecto", CodigosError.Fila.CampoRequerido,
                        "Un centro de costo de tipo PROYECTO requiere el proyecto."));
                else if (!esProyecto && fila.Proyecto is not null)
                    errores.Add(Error(fila, "Proyecto", CodigosError.Fila.ReglaNegocio,
                        $"Solo el tipo PROYECTO admite proyecto; deje la columna vacía para el tipo {tipo.Codigo}."));
            }

            tvps.Add(new CentroCostoImportTvpDto
            {
                Codigo = fila.Codigo,
                Nombre = fila.Nombre,
                IdTipoCentroCosto = tipo?.Id ?? 0,
                IdProyecto = proyecto?.Id,
                Descripcion = fila.Descripcion,
                _Fila = fila._Fila
            });
        }

        if (errores.Count > 0)
        {
            var ordenados = errores.OrderBy(e => e.Fila).ThenBy(e => e.Campo).ToList();
            throw new DatosInvalidosException(
                $"El archivo contiene {ordenados.Count} error(es). No se importó ningún centro de costo.", ordenados);
        }

        return tvps;
    }

    private static DetalleErrorFila Error(CentroCostoImportDto fila, string campo, string codigo, string mensaje)
        => new(fila._Fila, campo, codigo, mensaje);
}
