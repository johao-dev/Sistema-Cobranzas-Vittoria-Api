using Cobranzas_Vittoria.Dtos.Contable;
using Cobranzas_Vittoria.Interfaces;
using Cobranzas_Vittoria.Services;
using Microsoft.AspNetCore.Mvc;

namespace Cobranzas_Vittoria.Controllers;

[ApiController]
[Route("api/contable/gastos-proyecto/{tipoModulo}")]
public class GastosProyectoController : ControllerBase
{
    private readonly IGastoProyectoService _service;
    private readonly IWebHostEnvironment _env;

    public GastosProyectoController(IGastoProyectoService service, IWebHostEnvironment env)
    {
        _service = service;
        _env = env;
    }

    [HttpGet]
    public async Task<IActionResult> List(string tipoModulo, [FromQuery] int? idProyecto, [FromQuery] string? concepto, [FromQuery] string? estado, [FromQuery] bool? activo)
        => Ok(await _service.ListAsync(tipoModulo, idProyecto, concepto, estado, activo));

    [HttpGet("{gastoId:int}")]
    public async Task<IActionResult> Get(string tipoModulo, int gastoId)
    {
        _ = GastoProyectoService.NormalizeTipoModulo(tipoModulo);
        var response = await _service.GetAsync(gastoId);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> Create(string tipoModulo, [FromBody] GastoProyectoUpsertDto dto)
        => Ok(new { IdGastoProyecto = await _service.UpsertAsync(tipoModulo, dto) });

    [HttpPut("{gastoId:int}")]
    public async Task<IActionResult> Update(string tipoModulo, int gastoId, [FromBody] GastoProyectoUpsertDto dto)
    {
        dto.IdGastoProyecto = gastoId;
        return Ok(new { IdGastoProyecto = await _service.UpsertAsync(tipoModulo, dto) });
    }

    [HttpDelete("{gastoId:int}")]
    public async Task<IActionResult> Delete(string tipoModulo, int gastoId)
    {
        _ = GastoProyectoService.NormalizeTipoModulo(tipoModulo);
        await _service.DeleteAsync(gastoId);
        return Ok(new { ok = true });
    }

    [HttpGet("{gastoId:int}/documentos")]
    public async Task<IActionResult> GetDocumentos(string tipoModulo, int gastoId)
    {
        _ = GastoProyectoService.NormalizeTipoModulo(tipoModulo);
        return Ok(await _service.GetDocumentosAsync(gastoId));
    }

    [HttpPost("{gastoId:int}/documentos")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> UploadDocumentos(string tipoModulo, int gastoId, [FromForm] List<IFormFile> files)
    {
        if (files == null || files.Count == 0)
            return BadRequest(new { message = "Debes adjuntar una o más facturas en PDF." });

        var modulo = GastoProyectoService.NormalizeTipoModulo(tipoModulo).ToLowerInvariant();
        var root = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads", "gastos-proyecto", modulo, gastoId.ToString(), "facturas");
        Directory.CreateDirectory(root);

        var docs = new List<(string TipoDocumento, string NombreArchivo, string RutaArchivo, string? Extension)>();
        foreach (var file in files)
        {
            var ext = Path.GetExtension(file.FileName);
            if (!string.Equals(ext, ".pdf", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "Solo se permiten archivos PDF." });

            var safeName = $"{Guid.NewGuid():N}_{Path.GetFileName(file.FileName)}";
            var fullPath = Path.Combine(root, safeName);
            await using var stream = System.IO.File.Create(fullPath);
            await file.CopyToAsync(stream);

            var relative = Path.Combine("uploads", "gastos-proyecto", modulo, gastoId.ToString(), "facturas", safeName).Replace("\\", "/");
            docs.Add(("Factura", file.FileName, relative, ext));
        }

        await _service.SaveDocumentosAsync(gastoId, docs);
        return Ok(new { ok = true });
    }

    [HttpGet("{gastoId:int}/documentos/{documentoId:int}/download")]
    public async Task<IActionResult> DownloadDocumento(string tipoModulo, int gastoId, int documentoId)
    {
        _ = GastoProyectoService.NormalizeTipoModulo(tipoModulo);
        var docs = await _service.GetDocumentosAsync(gastoId);
        var documento = docs.FirstOrDefault(x => x.IdGastoProyectoDocumento == documentoId);
        if (documento is null)
            return NotFound(new { message = "No se encontró el documento solicitado." });

        var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var physicalPath = Path.Combine(webRoot, documento.RutaArchivo.Replace("/", Path.DirectorySeparatorChar.ToString()));
        if (!System.IO.File.Exists(physicalPath))
            return NotFound(new { message = "No se encontró el archivo físico del documento." });

        return PhysicalFile(physicalPath, "application/pdf", documento.NombreArchivo);
    }
}
