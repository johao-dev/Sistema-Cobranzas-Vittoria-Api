using Cobranzas_Vittoria.Dtos.Contable;
using Cobranzas_Vittoria.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Cobranzas_Vittoria.Controllers;

[ApiController]
[Route("api/contable/gastos-directos")]
public sealed class GastosDirectosController : ControllerBase
{
    private readonly IGastoDirectoService _service;
    private readonly IWebHostEnvironment _environment;

    public GastosDirectosController(IGastoDirectoService service, IWebHostEnvironment environment)
    {
        _service = service;
        _environment = environment;
    }

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] string? estado,
        [FromQuery] int? idProveedor, [FromQuery] int? idCentroCosto,
        [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
        => Ok(await _service.ListarAsync(estado, idProveedor, idCentroCosto, desde, hasta));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Obtener(int id)
    {
        var resultado = await _service.ObtenerAsync(id);
        return resultado is null ? NotFound() : Ok(resultado);
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] GastoDirectoUpsertDto dto)
    {
        var id = await _service.CrearAsync(dto);
        return CreatedAtAction(nameof(Obtener), new { id }, new { IdGastoDirecto = id });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Actualizar(int id, [FromBody] GastoDirectoUpsertDto dto)
        => Ok(new { IdGastoDirecto = await _service.ActualizarAsync(id, dto) });

    [HttpPost("{id:int}/confirmar")]
    public async Task<IActionResult> Confirmar(int id)
    {
        await _service.ConfirmarAsync(id);
        return Ok(new { ok = true, idGastoDirecto = id, estado = "CONFIRMADO" });
    }

    [HttpPost("{id:int}/anular")]
    public async Task<IActionResult> Anular(int id)
    {
        await _service.AnularAsync(id);
        return Ok(new { ok = true, idGastoDirecto = id, estado = "ANULADO" });
    }

    [HttpGet("{id:int}/documentos")]
    public async Task<IActionResult> ListarDocumentos(int id)
        => Ok(await _service.ListarDocumentosAsync(id));

    [HttpPost("{id:int}/documentos")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> SubirDocumentos(int id, [FromForm] string tipoDocumento,
        [FromForm] List<IFormFile> files)
    {
        if (files.Count == 0) return BadRequest(new { message = "Debe adjuntar al menos un PDF." });
        var tipo = string.Equals(tipoDocumento, "Pago", StringComparison.OrdinalIgnoreCase)
            ? "Pago" : string.Equals(tipoDocumento, "Factura", StringComparison.OrdinalIgnoreCase)
                ? "Factura" : null;
        if (tipo is null) return BadRequest(new { message = "TipoDocumento debe ser Factura o Pago." });
        if (files.Any(x => !string.Equals(Path.GetExtension(x.FileName), ".pdf", StringComparison.OrdinalIgnoreCase)))
            return BadRequest(new { message = "Solo se permiten archivos PDF." });

        var webRoot = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var root = Path.Combine(webRoot, "uploads", "gastos-directos", id.ToString(), tipo.ToLowerInvariant());
        Directory.CreateDirectory(root);
        var escritos = new List<string>();
        var documentos = new List<GastoDirectoDocumentoNuevo>();
        try
        {
            foreach (var file in files)
            {
                var safeName = $"{Guid.NewGuid():N}_{Path.GetFileName(file.FileName)}";
                var fullPath = Path.Combine(root, safeName);
                await using (var stream = System.IO.File.Create(fullPath))
                    await file.CopyToAsync(stream);
                escritos.Add(fullPath);
                var relative = Path.Combine("uploads", "gastos-directos", id.ToString(),
                    tipo.ToLowerInvariant(), safeName).Replace("\\", "/");
                documentos.Add(new(tipo, Path.GetFileName(file.FileName), relative, ".pdf"));
            }
            await _service.RegistrarDocumentosAsync(id, documentos);
            return Ok(new { ok = true, cantidad = documentos.Count });
        }
        catch
        {
            // Compensación local: si falla la metadata SQL, no quedan archivos huérfanos.
            foreach (var path in escritos)
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            throw;
        }
    }

    [HttpGet("{id:int}/documentos/{documentoId:int}/download")]
    public async Task<IActionResult> DescargarDocumento(int id, int documentoId)
    {
        var documento = (await _service.ListarDocumentosAsync(id))
            .FirstOrDefault(x => x.IdGastoDirectoDocumento == documentoId);
        if (documento is null) return NotFound();
        var webRoot = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var fullPath = Path.GetFullPath(Path.Combine(webRoot,
            documento.RutaArchivo.Replace('/', Path.DirectorySeparatorChar)));
        var root = Path.GetFullPath(webRoot) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(root, StringComparison.Ordinal) || !System.IO.File.Exists(fullPath))
            return NotFound(new { message = "No se encontró el archivo físico del documento." });
        return PhysicalFile(fullPath, "application/pdf", documento.NombreArchivo);
    }
}
