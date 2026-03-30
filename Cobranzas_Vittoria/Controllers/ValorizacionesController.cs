using Cobranzas_Vittoria.Dtos.Valorizaciones;
using Cobranzas_Vittoria.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Dapper;
using Cobranzas_Vittoria.Data;

namespace Cobranzas_Vittoria.Controllers
{
    [ApiController]
    [Route("api/contable/valorizaciones")]
    public class ValorizacionesController : ControllerBase
    {
        private readonly IValorizacionService _service;
        private readonly IWebHostEnvironment _env;
        private readonly IDbConnectionFactory _factory;

        public ValorizacionesController(IValorizacionService service, IWebHostEnvironment env, IDbConnectionFactory factory)
        {
            _service = service;
            _env = env;
            _factory = factory;
        }

        [HttpGet("configuraciones")]
        public async Task<IActionResult> ListConfiguraciones([FromQuery] int? idProyecto, [FromQuery] int? idProveedor, [FromQuery] int? idEspecialidad)
            => Ok(await _service.ListConfiguracionesAsync(idProyecto, idProveedor, idEspecialidad));

        [HttpPost("configuraciones")]
        public async Task<IActionResult> UpsertConfiguracion([FromBody] ProveedorEspecialidadCotizacionUpsertDto dto)
            => Ok(await _service.UpsertConfiguracionAsync(dto));

        [HttpPost("reglas-proveedor")]
        public async Task<IActionResult> UpsertReglaProveedor([FromBody] ProveedorReglaValorizacionUpsertDto dto)
            => Ok(await _service.UpsertReglaProveedorAsync(dto));

        [HttpGet]
        public async Task<IActionResult> List([FromQuery] int? idProyecto, [FromQuery] int? idProveedor, [FromQuery] int? idEspecialidad)
            => Ok(await _service.ListAsync(idProyecto, idProveedor, idEspecialidad));

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id)
            => Ok(await _service.GetByIdAsync(id));

        [HttpPost]
        public async Task<IActionResult> Upsert([FromBody] ValorizacionUpsertDto dto)
            => Ok(await _service.UpsertAsync(dto));

        [HttpPost("detalle")]
        public async Task<IActionResult> UpsertDetalle([FromBody] ValorizacionDetalleUpsertDto dto)
            => Ok(await _service.UpsertDetalleAsync(dto));

        [HttpDelete("detalle/{id:int}")]
        public async Task<IActionResult> DeleteDetalle(int id, [FromQuery] string usuario = "system")
            => Ok(await _service.DeleteDetalleAsync(id, usuario));

        [HttpPost("detalle/{idDetalle:int}/archivos")]
        [RequestSizeLimit(50_000_000)]
        public async Task<IActionResult> UploadArchivos(int idDetalle, [FromForm] List<IFormFile> files)
        {
            if (files == null || files.Count == 0)
                return BadRequest(new { message = "Debes adjuntar uno o más archivos PDF." });

            var root = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads", "valorizaciones", idDetalle.ToString());
            Directory.CreateDirectory(root);

            using var db = _factory.CreateConnection();
            foreach (var file in files)
            {
                var ext = Path.GetExtension(file.FileName);
                if (!string.Equals(ext, ".pdf", StringComparison.OrdinalIgnoreCase))
                    return BadRequest(new { message = "Solo se permiten archivos PDF." });

                var safeName = $"{Guid.NewGuid():N}_{Path.GetFileName(file.FileName)}";
                var fullPath = Path.Combine(root, safeName);

                await using var stream = System.IO.File.Create(fullPath);
                await file.CopyToAsync(stream);

                var relative = Path.Combine("uploads", "valorizaciones", idDetalle.ToString(), safeName).Replace("\\", "/");

                await db.ExecuteAsync(@"
INSERT INTO contable.ValorizacionDetalleArchivo
(
    IdValorizacionDetalle,
    NombreArchivo,
    RutaArchivo,
    Extension,
    FechaCreacion
)
VALUES
(
    @IdValorizacionDetalle,
    @NombreArchivo,
    @RutaArchivo,
    @Extension,
    GETDATE()
)", new
                {
                    IdValorizacionDetalle = idDetalle,
                    NombreArchivo = file.FileName,
                    RutaArchivo = relative,
                    Extension = ext
                });
            }

            return Ok(new { ok = true });
        }

        [HttpGet("detalle/{idDetalle:int}/archivos/{idArchivo:int}/download")]
        public async Task<IActionResult> DownloadArchivo(int idDetalle, int idArchivo)
        {
            using var db = _factory.CreateConnection();
            var doc = await db.QueryFirstOrDefaultAsync(@"
SELECT
    IdValorizacionDetalleArchivo,
    IdValorizacionDetalle,
    NombreArchivo,
    RutaArchivo,
    Extension
FROM contable.ValorizacionDetalleArchivo
WHERE IdValorizacionDetalleArchivo = @IdArchivo
  AND IdValorizacionDetalle = @IdDetalle", new { IdArchivo = idArchivo, IdDetalle = idDetalle });

            if (doc == null)
                return NotFound(new { message = "No se encontró el archivo solicitado." });

            var rutaRelativa = (string?)doc.RutaArchivo;
            var nombreArchivo = (string?)doc.NombreArchivo ?? $"factura_{idArchivo}.pdf";

            if (string.IsNullOrWhiteSpace(rutaRelativa))
                return NotFound(new { message = "El archivo no tiene ruta registrada." });

            var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var rutaFisica = Path.Combine(webRoot, rutaRelativa.Replace("/", Path.DirectorySeparatorChar.ToString()));

            if (!System.IO.File.Exists(rutaFisica))
                return NotFound(new { message = "No se encontró el archivo físico." });

            return PhysicalFile(rutaFisica, "application/pdf", nombreArchivo);
        }
    }
}
