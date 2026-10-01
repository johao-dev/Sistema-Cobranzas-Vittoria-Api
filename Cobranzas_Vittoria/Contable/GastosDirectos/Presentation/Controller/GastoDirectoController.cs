using Cobranzas_Vittoria.Seguridad.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cobranzas_Vittoria.Contable.GastosDirectos.Application.Actualizar;
using Cobranzas_Vittoria.Contable.GastosDirectos.Application.Anular;
using Cobranzas_Vittoria.Contable.GastosDirectos.Application.CentrosCosto;
using Cobranzas_Vittoria.Contable.GastosDirectos.Application.Common;
using Cobranzas_Vittoria.Contable.GastosDirectos.Application.Confirmar;
using Cobranzas_Vittoria.Contable.GastosDirectos.Application.Crear;
using Cobranzas_Vittoria.Contable.GastosDirectos.Application.Documentos.Descargar;
using Cobranzas_Vittoria.Contable.GastosDirectos.Application.Documentos.Listar;
using Cobranzas_Vittoria.Contable.GastosDirectos.Application.Documentos.Subir;
using Cobranzas_Vittoria.Contable.GastosDirectos.Application.Listar;
using Cobranzas_Vittoria.Contable.GastosDirectos.Application.Obtener;
using Cobranzas_Vittoria.Contable.GastosDirectos.Application.PartidasDisponibles;
using Cobranzas_Vittoria.Contable.GastosDirectos.Application.Proveedores;
using Cobranzas_Vittoria.Contable.GastosDirectos.Presentation.Dto;
using GD = Cobranzas_Vittoria.Seguridad.Authorization.Permisos.GastoDirecto;

namespace Cobranzas_Vittoria.Contable.GastosDirectos.Presentation.Controller;

/// <summary>
/// Gastos directos. Se registran desde cada sección de Operaciones → Gastos del proyecto
/// (parámetro seccion). Consultar exige gasto_directo.ver y operar exige gasto_directo.operar.
/// </summary>
[ApiController]
[Route("api/contable/gastos-directos")]
public sealed class GastoDirectoController : ControllerBase
{
    [HttpGet]
    [AuthorizePermission(GD.Ver)]
    public async Task<IActionResult> Listar([FromServices] ListarGastosDirectosHandler handler, [FromQuery] string? estado,
        [FromQuery] int? idProveedor, [FromQuery] int? idCentroCosto, [FromQuery] DateTime? desde, [FromQuery] DateTime? hasta,
        [FromQuery] string? seccion)
        => Ok(await handler.HandleAsync(new ListarGastosDirectosQuery(estado, idProveedor, idCentroCosto, desde, hasta, seccion)));

    /// <summary>Centros de costo que admite la sección (por tipo de centro de costo).</summary>
    [HttpGet("centros-costo")]
    [AuthorizePermission(GD.Ver)]
    public async Task<IActionResult> CentrosCosto([FromServices] ListarCentrosCostoSeccionHandler handler, [FromQuery] string? seccion)
        => Ok(await handler.HandleAsync(new ListarCentrosCostoSeccionQuery(seccion)));

    /// <summary>Proveedores activos: primero los de las categorías antiguas de la sección (deLaSeccion = true).</summary>
    [HttpGet("proveedores")]
    [AuthorizePermission(GD.Ver)]
    public async Task<IActionResult> Proveedores([FromServices] ListarProveedoresSeccionHandler handler, [FromQuery] string? seccion)
        => Ok(await handler.HandleAsync(new ListarProveedoresSeccionQuery(seccion)));

    /// <summary>Partidas de la sección con su saldo en la versión aprobada del centro de costo.</summary>
    [HttpGet("partidas-disponibles")]
    [AuthorizePermission(GD.Ver)]
    public async Task<IActionResult> PartidasDisponibles([FromServices] ListarPartidasDisponiblesHandler handler,
        [FromQuery] string? seccion, [FromQuery] int idCentroCosto)
        => Ok(await handler.HandleAsync(new ListarPartidasDisponiblesQuery(seccion, idCentroCosto)));

    [HttpGet("{id:int}", Name = "ObtenerGastoDirecto")]
    [AuthorizePermission(GD.Ver)]
    public async Task<IActionResult> Obtener([FromServices] ObtenerGastoDirectoHandler handler, int id)
    {
        var r = await handler.HandleAsync(new ObtenerGastoDirectoQuery(id));
        return Ok(new { gasto = r.Gasto, documentos = r.Documentos });
    }

    [HttpPost]
    [AuthorizePermission(GD.Operar)]
    public async Task<IActionResult> Crear([FromServices] CrearGastoDirectoHandler handler, [FromBody] GastoDirectoUpsertRequest r)
    {
        var id = await handler.HandleAsync(new CrearGastoDirectoCommand(r.IdPresupuestoDetalle, r.IdProveedor, r.IdMoneda, r.Fecha,
            r.Concepto, r.Descripcion, r.Monto, r.Seccion, r.IdMonedaOriginal, r.MontoOriginal, r.TipoCambio, r.FechaTipoCambio));
        return CreatedAtRoute("ObtenerGastoDirecto", new { id }, new { IdGastoDirecto = id });
    }

    [HttpPut("{id:int}")]
    [AuthorizePermission(GD.Operar)]
    public async Task<IActionResult> Actualizar([FromServices] ActualizarGastoDirectoHandler handler, int id,
        [FromBody] GastoDirectoUpsertRequest r)
        => Ok(new
        {
            IdGastoDirecto = await handler.HandleAsync(new ActualizarGastoDirectoCommand(id, r.IdPresupuestoDetalle, r.IdProveedor,
                r.IdMoneda, r.Fecha, r.Concepto, r.Descripcion, r.Monto, r.Seccion, r.IdMonedaOriginal, r.MontoOriginal,
                r.TipoCambio, r.FechaTipoCambio))
        });

    [HttpPost("{id:int}/confirmar")]
    [AuthorizePermission(GD.Operar)]
    public async Task<IActionResult> Confirmar([FromServices] ConfirmarGastoDirectoHandler handler, int id)
    {
        await handler.HandleAsync(new ConfirmarGastoDirectoCommand(id));
        return Ok(new { ok = true, idGastoDirecto = id, estado = "CONFIRMADO" });
    }

    [HttpPost("{id:int}/anular")]
    [AuthorizePermission(GD.Operar)]
    public async Task<IActionResult> Anular([FromServices] AnularGastoDirectoHandler handler, int id)
    {
        await handler.HandleAsync(new AnularGastoDirectoCommand(id));
        return Ok(new { ok = true, idGastoDirecto = id, estado = "ANULADO" });
    }

    [HttpGet("{id:int}/documentos")]
    [AuthorizePermission(GD.Ver)]
    public async Task<IActionResult> ListarDocumentos([FromServices] ListarDocumentosHandler handler, int id)
        => Ok(await handler.HandleAsync(new ListarDocumentosQuery(id)));

    [HttpPost("{id:int}/documentos")]
    [AuthorizePermission(GD.Operar)]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> SubirDocumentos([FromServices] SubirDocumentosHandler handler, int id,
        [FromForm] string? tipoDocumento, [FromForm] List<IFormFile> files, CancellationToken ct)
    {
        var archivos = new List<ArchivoAdjunto>(files.Count);
        foreach (var file in files)
        {
            await using var flujo = new MemoryStream();
            await file.CopyToAsync(flujo, ct);
            archivos.Add(new ArchivoAdjunto(file.FileName, flujo.ToArray()));
        }
        var cantidad = await handler.HandleAsync(new SubirDocumentosCommand(id, tipoDocumento, archivos));
        return Ok(new { ok = true, cantidad });
    }

    [HttpGet("{id:int}/documentos/{documentoId:int}/download")]
    [AuthorizePermission(GD.Ver)]
    public async Task<IActionResult> DescargarDocumento([FromServices] DescargarDocumentoHandler handler, int id, int documentoId)
    {
        var documento = await handler.HandleAsync(new DescargarDocumentoQuery(id, documentoId));
        return PhysicalFile(documento.RutaFisica, "application/pdf", documento.NombreArchivo);
    }
}
