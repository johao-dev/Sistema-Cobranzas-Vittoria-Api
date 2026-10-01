using Cobranzas_Vittoria.Seguridad.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cobranzas_Vittoria.Application.Common.Exports;
using Cobranzas_Vittoria.ControlPresupuestario.Application.CentroCosto.Actualizar;
using Cobranzas_Vittoria.ControlPresupuestario.Application.CentroCosto.Crear;
using Cobranzas_Vittoria.ControlPresupuestario.Application.CentroCosto.Importar;
using Cobranzas_Vittoria.ControlPresupuestario.Application.CentroCosto.Listar;
using Cobranzas_Vittoria.ControlPresupuestario.Application.CentroCosto.Obtener;
using Cobranzas_Vittoria.ControlPresupuestario.Application.CentroCosto.Plantilla;
using Cobranzas_Vittoria.ControlPresupuestario.Presentation.Dto.CentroCosto;
using Cobranzas_Vittoria.ControlPresupuestario.Presentation.Http;

namespace Cobranzas_Vittoria.ControlPresupuestario.Presentation.Controller;

/// <summary>Centros de costo: proyectos y áreas corporativas sobre los que se planifica el presupuesto.</summary>
[ApiController]
[Route("api/control-presupuestario/centros-costo")]
public sealed class CentroCostoController : ControllerBase
{
    private const long MaxRequestSize = 11_000_000;

    [HttpGet]
    [AuthorizePermission(Permisos.ControlPresupuestario.CentroCosto.Ver)]
    public async Task<IActionResult> Listar([FromServices] ListarCentroCostoHandler handler, [FromQuery] bool? activo,
        [FromQuery] int? idTipoCentroCosto, [FromQuery] int? idProyecto, [FromQuery] string? busqueda)
    {
        var centros = await handler.HandleAsync(new ListarCentroCostoQuery(activo, idTipoCentroCosto, idProyecto, busqueda));
        return Ok(centros.Select(CentroCostoResponse.Desde));
    }

    [HttpGet("{id:int}", Name = "ObtenerCentroCosto")]
    [AuthorizePermission(Permisos.ControlPresupuestario.CentroCosto.Ver)]
    public async Task<IActionResult> Obtener([FromServices] ObtenerCentroCostoHandler handler, int id)
        => Ok(CentroCostoResponse.Desde(await handler.HandleAsync(new ObtenerCentroCostoQuery(id))));

    [HttpPost]
    [AuthorizePermission(Permisos.ControlPresupuestario.CentroCosto.Crear)]
    public async Task<IActionResult> Crear([FromServices] CrearCentroCostoHandler handler, [FromBody] CrearCentroCostoRequest request)
    {
        var creado = await handler.HandleAsync(new CrearCentroCostoCommand(request.Codigo, request.Nombre,
            request.IdTipoCentroCosto, request.Descripcion, request.IdProyecto));
        return CreatedAtRoute("ObtenerCentroCosto", new { id = creado.IdCentroCosto }, CentroCostoResponse.Desde(creado));
    }

    [HttpPut("{id:int}")]
    [AuthorizePermission(Permisos.ControlPresupuestario.CentroCosto.Actualizar)]
    public async Task<IActionResult> Actualizar([FromServices] ActualizarCentroCostoHandler handler, int id,
        [FromBody] ActualizarCentroCostoRequest request)
    {
        var actualizado = await handler.HandleAsync(new ActualizarCentroCostoCommand(id, request.Nombre, request.Activo,
            request.Descripcion, request.IdProyecto, request.Codigo, request.IdTipoCentroCosto));
        return Ok(CentroCostoResponse.Desde(actualizado));
    }

    /// <summary>Importa centros de costo desde CSV/XLSX. Todo o nada: con un error no se inserta ninguno (422).</summary>
    [HttpPost("importar")]
    [AuthorizePermission(Permisos.ControlPresupuestario.CentroCosto.Crear)]
    [RequestSizeLimit(MaxRequestSize)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Importar([FromServices] ImportarCentroCostoHandler handler, IFormFile? archivo,
        CancellationToken ct)
        => Ok(await handler.HandleAsync(new ImportarCentroCostoCommand(await ArchivoSubido.LeerAsync(archivo, ct)), ct));

    [HttpGet("plantilla")]
    [AuthorizePermission(Permisos.ControlPresupuestario.CentroCosto.Crear)]
    public IActionResult Plantilla([FromServices] ObtenerPlantillaCentroCostoHandler handler,
        [FromServices] IExcelExporter excel, [FromQuery] string? formato = "csv")
    {
        var tipo = PlantillaArchivo.NormalizarFormato(formato);
        var nombre = PlantillaArchivo.NombreArchivo("plantilla-centros-costo", tipo);
        if (tipo == "csv")
            return File(PlantillaArchivo.Csv(handler.Handle(new ObtenerPlantillaCentroCostoQuery())), PlantillaArchivo.TipoCsv, nombre);
        var xlsx = excel.ExportToXlsx(Array.Empty<CentroCostoPlantillaFila>(), new ExcelSheetConfig
        {
            SheetName = "Plantilla Centros de costo",
            Title = "Plantilla de importacion - Centros de costo",
            FiltersSubtitle = "Tipo acepta el codigo o el nombre. Proyecto (nombre) solo para el tipo PROYECTO, y es obligatorio en ese caso.",
            GeneratedAtSubtitle = "Generado el: {0}",
            IncludeTotalsRow = false,
            HeaderRowIndex = 0
        });
        return File(xlsx, PlantillaArchivo.TipoXlsx, nombre);
    }
}
