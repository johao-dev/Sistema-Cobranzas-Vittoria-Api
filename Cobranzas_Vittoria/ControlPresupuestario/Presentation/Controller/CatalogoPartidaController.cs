using Cobranzas_Vittoria.Seguridad.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cobranzas_Vittoria.Application.Common.Exports;
using Cobranzas_Vittoria.ControlPresupuestario.Application.CatalogoPartida.Actualizar;
using Cobranzas_Vittoria.ControlPresupuestario.Application.CatalogoPartida.Crear;
using Cobranzas_Vittoria.ControlPresupuestario.Application.CatalogoPartida.Importar;
using Cobranzas_Vittoria.ControlPresupuestario.Application.CatalogoPartida.Listar;
using Cobranzas_Vittoria.ControlPresupuestario.Application.CatalogoPartida.Obtener;
using Cobranzas_Vittoria.ControlPresupuestario.Application.CatalogoPartida.Plantilla;
using Cobranzas_Vittoria.ControlPresupuestario.Presentation.Dto.CatalogoPartida;
using Cobranzas_Vittoria.ControlPresupuestario.Presentation.Http;

namespace Cobranzas_Vittoria.ControlPresupuestario.Presentation.Controller;

/// <summary>Catálogo reusable de partidas (no las partidas presupuestadas dentro de una versión).</summary>
[ApiController]
[Route("api/control-presupuestario/partidas")]
public sealed class CatalogoPartidaController : ControllerBase
{
    private const long MaxRequestSize = 11_000_000;

    [HttpGet]
    [AuthorizePermission(Permisos.ControlPresupuestario.Partida.Ver)]
    public async Task<IActionResult> Listar([FromServices] ListarCatalogoPartidaHandler handler, [FromQuery] bool? activo,
        [FromQuery] int? idTipoPartida, [FromQuery] int? idPartidaPadre, [FromQuery] bool soloRaices = false,
        [FromQuery] bool? esHoja = null, [FromQuery] string? busqueda = null, [FromQuery] int? idSeccionGasto = null)
    {
        var partidas = await handler.HandleAsync(new ListarCatalogoPartidaQuery(activo, idTipoPartida, idPartidaPadre,
            soloRaices, esHoja, busqueda, idSeccionGasto));
        return Ok(partidas.Select(CatalogoPartidaResponse.Desde));
    }

    [HttpGet("{id:int}", Name = "ObtenerCatalogoPartida")]
    [AuthorizePermission(Permisos.ControlPresupuestario.Partida.Ver)]
    public async Task<IActionResult> Obtener([FromServices] ObtenerCatalogoPartidaHandler handler, int id)
        => Ok(CatalogoPartidaResponse.Desde(await handler.HandleAsync(new ObtenerCatalogoPartidaQuery(id))));

    [HttpPost]
    [AuthorizePermission(Permisos.ControlPresupuestario.Partida.Crear)]
    public async Task<IActionResult> Crear([FromServices] CrearCatalogoPartidaHandler handler,
        [FromBody] CrearCatalogoPartidaRequest request)
    {
        var creada = await handler.HandleAsync(new CrearCatalogoPartidaCommand(request.Codigo, request.Nombre,
            request.IdTipoPartida, request.IdPartidaPadre, request.Descripcion, request.IdSeccionGasto));
        return CreatedAtRoute("ObtenerCatalogoPartida", new { id = creada.IdCatalogoPartida }, CatalogoPartidaResponse.Desde(creada));
    }

    [HttpPut("{id:int}")]
    [AuthorizePermission(Permisos.ControlPresupuestario.Partida.Actualizar)]
    public async Task<IActionResult> Actualizar([FromServices] ActualizarCatalogoPartidaHandler handler, int id,
        [FromBody] ActualizarCatalogoPartidaRequest request)
    {
        var actualizada = await handler.HandleAsync(new ActualizarCatalogoPartidaCommand(id, request.Nombre,
            request.IdTipoPartida, request.Activo, request.IdPartidaPadre, request.Descripcion, request.IdSeccionGasto));
        return Ok(CatalogoPartidaResponse.Desde(actualizada));
    }

    /// <summary>Importa partidas desde CSV/XLSX. Las hijas pueden ir antes que su padre. Todo o nada (422).</summary>
    [HttpPost("importar")]
    [AuthorizePermission(Permisos.ControlPresupuestario.Partida.Crear)]
    [RequestSizeLimit(MaxRequestSize)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Importar([FromServices] ImportarCatalogoPartidaHandler handler, IFormFile? archivo,
        CancellationToken ct)
        => Ok(await handler.HandleAsync(new ImportarCatalogoPartidaCommand(await ArchivoSubido.LeerAsync(archivo, ct)), ct));

    [HttpGet("plantilla")]
    [AuthorizePermission(Permisos.ControlPresupuestario.Partida.Crear)]
    public IActionResult Plantilla([FromServices] ObtenerPlantillaCatalogoPartidaHandler handler,
        [FromServices] IExcelExporter excel, [FromQuery] string? formato = "csv")
    {
        var tipo = PlantillaArchivo.NormalizarFormato(formato);
        var nombre = PlantillaArchivo.NombreArchivo("plantilla-partidas", tipo);
        if (tipo == "csv")
            return File(PlantillaArchivo.Csv(handler.Handle(new ObtenerPlantillaCatalogoPartidaQuery())), PlantillaArchivo.TipoCsv, nombre);
        var xlsx = excel.ExportToXlsx(Array.Empty<CatalogoPartidaPlantillaFila>(), new ExcelSheetConfig
        {
            SheetName = "Plantilla Partidas",
            Title = "Plantilla de importacion - Catalogo de partidas",
            FiltersSubtitle = "Tipo y Seccion aceptan el codigo o el nombre. CodigoPadre puede ser otra fila del archivo.",
            GeneratedAtSubtitle = "Generado el: {0}",
            IncludeTotalsRow = false,
            HeaderRowIndex = 0
        });
        return File(xlsx, PlantillaArchivo.TipoXlsx, nombre);
    }
}
