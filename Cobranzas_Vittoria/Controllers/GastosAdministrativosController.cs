using Cobranzas_Vittoria.Dtos.GastosAdministrativos;
using Cobranzas_Vittoria.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Cobranzas_Vittoria.Controllers;

[ApiController]
[Route("api/contable/gastos-administrativos")]
public class GastosAdministrativosController : ControllerBase
{
    private readonly IGastoAdministrativoService _service;
    private readonly IWebHostEnvironment _env;

    public GastosAdministrativosController(IGastoAdministrativoService service, IWebHostEnvironment env)
    {
        _service = service;
        _env = env;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int? idCategoriaGasto, [FromQuery] int? idProveedorGastoAdministrativo, [FromQuery] bool? activo)
        => Ok(await _service.ListAsync(idCategoriaGasto, idProveedorGastoAdministrativo, activo));

    [HttpGet("{gastoId:int}")]
    public async Task<IActionResult> Get(int gastoId)
    {
        var response = await _service.GetAsync(gastoId);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] GastoAdministrativoUpsertDto dto)
        => Ok(new { IdGastoAdministrativo = await _service.UpsertAsync(dto) });

    [HttpPut("{gastoId:int}")]
    public async Task<IActionResult> Update(int gastoId, [FromBody] GastoAdministrativoUpsertDto dto)
    {
        dto.IdGastoAdministrativo = gastoId;
        return Ok(new { IdGastoAdministrativo = await _service.UpsertAsync(dto) });
    }

    [HttpDelete("{gastoId:int}")]
    public async Task<IActionResult> Delete(int gastoId)
    {
        await _service.DeleteAsync(gastoId);
        return Ok(new { ok = true });
    }

    [HttpGet("{gastoId:int}/documentos")]
    public async Task<IActionResult> GetDocumentos(int gastoId)
        => Ok(await _service.GetDocumentosAsync(gastoId));

    [HttpPost("{gastoId:int}/documentos")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> UploadDocumentos(int gastoId, [FromForm] string tipoDocumento, [FromForm] List<IFormFile> files)
    {
        if (files == null || files.Count == 0)
            return BadRequest(new { message = "Debes adjuntar uno o más archivos PDF." });

        var tipo = string.Equals(tipoDocumento, "Pago", StringComparison.OrdinalIgnoreCase) ? "Pago" : "Factura";
        var root = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads", "gastos-administrativos", gastoId.ToString(), tipo.ToLowerInvariant());
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

            var relative = Path.Combine("uploads", "gastos-administrativos", gastoId.ToString(), tipo.ToLowerInvariant(), safeName).Replace("\\", "/");
            docs.Add((tipo, file.FileName, relative, ext));
        }

        await _service.SaveDocumentosAsync(gastoId, docs);
        return Ok(new { ok = true });
    }

    [HttpGet("{gastoId:int}/documentos/{documentoId:int}/download")]
    public async Task<IActionResult> DownloadDocumento(int gastoId, int documentoId)
    {
        var docs = await _service.GetDocumentosAsync(gastoId);
        var documento = docs.FirstOrDefault(x => x.IdGastoAdministrativoDocumento == documentoId);
        if (documento is null)
            return NotFound(new { message = "No se encontró el documento solicitado." });

        var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var physicalPath = Path.Combine(webRoot, documento.RutaArchivo.Replace("/", Path.DirectorySeparatorChar.ToString()));
        if (!System.IO.File.Exists(physicalPath))
            return NotFound(new { message = "No se encontró el archivo físico del documento." });

        return PhysicalFile(physicalPath, "application/pdf", documento.NombreArchivo);
    }
}
