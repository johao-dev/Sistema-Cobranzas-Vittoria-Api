using Cobranzas_Vittoria.Application.Compras;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Dtos.Compras;
using Cobranzas_Vittoria.Interfaces;
using Dapper;
using System.Data;
using System.IO;
using System.Linq;

namespace Cobranzas_Vittoria.Repositories
{
    public class CompraRepository : RepositoryBase, ICompraRepository
    {
        public CompraRepository(IDbConnectionFactory factory) : base(factory) { }

        public async Task<IEnumerable<dynamic>> ListAsync(bool? aceptada, int? idProveedor)
        {
            using var db = Open();

            var sql = @"
SELECT
    c.IdCompra,
    c.NumeroCompra,
    c.FechaCompra,
    c.Estado,
    c.Aceptada,
    c.IncluyeIGV,
    c.SubtotalSinIGV,
    c.MontoIGV,
    c.MontoTotal,
    c.Observacion,
    c.IdOrdenCompra,
    oc.NumeroOrdenCompra,
    oc.IdMoneda,
    mon.Codigo AS CodigoMoneda,
    mon.Simbolo AS SimboloMoneda,
    provAgg.Proveedores,
    COALESCE(NULLIF(LTRIM(RTRIM(r.NumeroRequerimiento)), ''), '-') AS NumeroRequerimiento,
    COALESCE(NULLIF(LTRIM(RTRIM(pr.NombreProyecto)), ''), '-') AS NombreProyecto,
    COALESCE(espAgg.Especialidad, '-') AS Especialidades
FROM compras.Compra c
INNER JOIN compras.OrdenCompra oc ON oc.IdOrdenCompra = c.IdOrdenCompra
INNER JOIN maestra.Moneda mon ON mon.IdMoneda = oc.IdMoneda
LEFT JOIN compras.Requerimiento r ON r.IdRequerimiento = oc.IdRequerimiento
LEFT JOIN maestra.Proyecto pr ON pr.IdProyecto = r.IdProyecto
OUTER APPLY
(
    SELECT STRING_AGG(CONVERT(NVARCHAR(MAX), q.RazonSocial), ', ') AS Proveedores
    FROM (
        SELECT DISTINCT p2.IdProveedor, p2.RazonSocial
        FROM compras.OrdenCompraDetalle od
        INNER JOIN maestra.Proveedor p2 ON p2.IdProveedor = od.IdProveedor
        WHERE od.IdOrdenCompra = oc.IdOrdenCompra
          AND EXISTS (SELECT 1 FROM compras.CompraDetalle cd WHERE cd.IdCompra = c.IdCompra AND cd.IdMaterial = od.IdMaterial)
    ) q
) provAgg
OUTER APPLY
(
    SELECT STRING_AGG(x.Nombre, ', ') AS Especialidad
    FROM
    (
        SELECT DISTINCT e2.Nombre
        FROM compras.OrdenCompraDetalle od
        INNER JOIN maestra.Material m ON m.IdMaterial = od.IdMaterial
        INNER JOIN maestra.Especialidad e2 ON e2.IdEspecialidad = m.IdEspecialidad
        WHERE od.IdOrdenCompra = oc.IdOrdenCompra
    ) x
) espAgg
WHERE (@Aceptada IS NULL OR c.Aceptada = @Aceptada)
  AND (@IdProveedor IS NULL OR EXISTS (
      SELECT 1 FROM compras.CompraDetalle cd
      INNER JOIN compras.OrdenCompraDetalle od ON od.IdOrdenCompra = c.IdOrdenCompra AND od.IdMaterial = cd.IdMaterial
      WHERE cd.IdCompra = c.IdCompra AND od.IdProveedor = @IdProveedor))
ORDER BY c.IdCompra DESC;";

            return await db.QueryAsync(sql, new { Aceptada = aceptada, IdProveedor = idProveedor });
        }

        public async Task<(int IdCompra, decimal MontoTotal)> CrearAsync(CompraCreateDto dto)
        {
            using var db = Open();
            ComprasNumeros.ValidarMateriales(dto.Items.Select(item => item.IdMaterial));
            ComprasNumeros.ValidarImportes(dto.Items.Select(item => (item.Cantidad, item.PrecioUnitario)));
            var numeroCompra = await EnsureNumeroCompraAsync(db, null, (dto.NumeroCompra ?? string.Empty).Trim());
            var fechaCompra = dto.FechaCompra == default ? DateTime.Today : dto.FechaCompra.Date;

            var items = new DataTable();
            items.Columns.Add("IdMaterial", typeof(int));
            items.Columns.Add("Cantidad", typeof(decimal));
            items.Columns.Add("PrecioUnitario", typeof(decimal));
            foreach (var item in dto.Items)
                items.Rows.Add(item.IdMaterial, item.Cantidad, item.PrecioUnitario);

            var documentos = new DataTable();
            documentos.Columns.Add("TipoDocumento", typeof(string));
            documentos.Columns.Add("NumeroDocumento", typeof(string));
            documentos.Columns.Add("RutaArchivo", typeof(string));
            documentos.Columns.Add("FechaDocumento", typeof(DateTime));
            documentos.Columns.Add("Monto", typeof(decimal));
            documentos.Columns.Add("Observacion", typeof(string));

            var parametros = new DynamicParameters();
            parametros.Add("NumeroCompra", numeroCompra);
            parametros.Add("IdOrdenCompra", dto.IdOrdenCompra);
            parametros.Add("FechaCompra", fechaCompra);
            parametros.Add("IncluyeIGV", dto.IncluyeIGV);
            parametros.Add("Observacion", dto.Observacion);
            parametros.Add("Items", items.AsTableValuedParameter("compras.TVP_CompraDetalle"));
            parametros.Add("Documentos", documentos.AsTableValuedParameter("compras.TVP_CompraDocumento"));

            var resultado = await db.QuerySingleAsync<dynamic>(
                "compras.usp_Compra_Registrar", parametros, commandType: CommandType.StoredProcedure);
            return ((int)resultado.IdCompra, (decimal)resultado.MontoTotal);
        }

        public async Task AceptarAsync(int idCompra, int? idUsuario, string? observacion)
        {
            using var db = Open();
            await db.ExecuteAsync("compras.usp_Compra_Aceptar",
                new { IdCompra = idCompra, IdUsuario = idUsuario, Observacion = observacion },
                commandType: CommandType.StoredProcedure);
        }

        public async Task<IEnumerable<dynamic>> ListPendientesDesdeOcAsync()
        {
            using var db = Open();

            var sql = @"
SELECT
    oc.IdOrdenCompra,
    oc.NumeroOrdenCompra,
    oc.IdMoneda,
    mon.Codigo AS CodigoMoneda,
    mon.Simbolo AS SimboloMoneda,
    oc.FechaOrdenCompra,
    oc.Estado,
    oc.Total,
    provAgg.Proveedores,
    r.IdRequerimiento,
    COALESCE(NULLIF(LTRIM(RTRIM(r.NumeroRequerimiento)), ''), '-') AS NumeroRequerimiento,
    COALESCE(espAgg.Especialidad, '-') AS Especialidades,
    COALESCE(NULLIF(LTRIM(RTRIM(pr.NombreProyecto)), ''), '-') AS NombreProyecto
FROM compras.OrdenCompra oc
INNER JOIN maestra.Moneda mon ON mon.IdMoneda = oc.IdMoneda
LEFT JOIN compras.Requerimiento r ON r.IdRequerimiento = oc.IdRequerimiento
LEFT JOIN maestra.Proyecto pr ON pr.IdProyecto = r.IdProyecto
OUTER APPLY
(
    SELECT STRING_AGG(CONVERT(NVARCHAR(MAX), q.RazonSocial), ', ') AS Proveedores
    FROM (
        SELECT DISTINCT p2.IdProveedor, p2.RazonSocial
        FROM compras.OrdenCompraDetalle od
        INNER JOIN maestra.Proveedor p2 ON p2.IdProveedor = od.IdProveedor
        WHERE od.IdOrdenCompra = oc.IdOrdenCompra
    ) q
) provAgg
OUTER APPLY
(
    SELECT STRING_AGG(x.Nombre, ', ') AS Especialidad
    FROM
    (
        SELECT DISTINCT e2.Nombre
        FROM compras.OrdenCompraDetalle od
        INNER JOIN maestra.Material m ON m.IdMaterial = od.IdMaterial
        INNER JOIN maestra.Especialidad e2 ON e2.IdEspecialidad = m.IdEspecialidad
        WHERE od.IdOrdenCompra = oc.IdOrdenCompra
    ) x
) espAgg
LEFT JOIN compras.Compra c ON c.IdOrdenCompra = oc.IdOrdenCompra
WHERE c.IdCompra IS NULL
ORDER BY oc.IdOrdenCompra DESC;";

            return await db.QueryAsync(sql);
        }

        public async Task<object?> GetAsync(int idCompra)
        {
            using var db = Open();

            var compra = await db.QueryFirstOrDefaultAsync(@"
SELECT
    c.IdCompra,
    c.NumeroCompra,
    c.FechaCompra,
    c.Estado,
    c.Aceptada,
    c.IncluyeIGV,
    c.SubtotalSinIGV,
    c.MontoIGV,
    c.MontoTotal,
    c.Observacion,
    c.IdOrdenCompra,
    oc.NumeroOrdenCompra,
    oc.IdMoneda,
    mon.Codigo AS CodigoMoneda,
    mon.Simbolo AS SimboloMoneda,
    COALESCE(NULLIF(LTRIM(RTRIM(r.NumeroRequerimiento)), ''), '-') AS NumeroRequerimiento,
    provAgg.Proveedores,
    COALESCE(espAgg.Especialidad, '-') AS Especialidades,
    COALESCE(NULLIF(LTRIM(RTRIM(pr.NombreProyecto)), ''), '-') AS NombreProyecto
FROM compras.Compra c
INNER JOIN compras.OrdenCompra oc ON oc.IdOrdenCompra = c.IdOrdenCompra
INNER JOIN maestra.Moneda mon ON mon.IdMoneda = oc.IdMoneda
LEFT JOIN compras.Requerimiento r ON r.IdRequerimiento = oc.IdRequerimiento
LEFT JOIN maestra.Proyecto pr ON pr.IdProyecto = r.IdProyecto
OUTER APPLY
(
    SELECT STRING_AGG(CONVERT(NVARCHAR(MAX), q.RazonSocial), ', ') AS Proveedores
    FROM (
        SELECT DISTINCT p2.IdProveedor, p2.RazonSocial
        FROM compras.OrdenCompraDetalle od
        INNER JOIN maestra.Proveedor p2 ON p2.IdProveedor = od.IdProveedor
        WHERE od.IdOrdenCompra = oc.IdOrdenCompra
          AND EXISTS (SELECT 1 FROM compras.CompraDetalle cd WHERE cd.IdCompra = c.IdCompra AND cd.IdMaterial = od.IdMaterial)
    ) q
) provAgg
OUTER APPLY
(
    SELECT STRING_AGG(x.Nombre, ', ') AS Especialidad
    FROM
    (
        SELECT DISTINCT e2.Nombre
        FROM compras.OrdenCompraDetalle od
        INNER JOIN maestra.Material m ON m.IdMaterial = od.IdMaterial
        INNER JOIN maestra.Especialidad e2 ON e2.IdEspecialidad = m.IdEspecialidad
        WHERE od.IdOrdenCompra = oc.IdOrdenCompra
    ) x
) espAgg
WHERE c.IdCompra = @IdCompra;", new { IdCompra = idCompra });

            if (compra == null)
                return null;

            var items = await db.QueryAsync(@"
SELECT
    d.IdCompraDetalle,
    d.IdCompra,
    d.IdMaterial,
    m.Descripcion AS Material,
    m.UnidadMedida,
    d.Cantidad,
    d.PrecioUnitario,
    d.Subtotal,
    od.IdProveedor AS IdProveedor,
    p.RazonSocial AS Proveedor
FROM compras.CompraDetalle d
INNER JOIN maestra.Material m ON m.IdMaterial = d.IdMaterial
INNER JOIN compras.Compra c ON c.IdCompra = d.IdCompra
INNER JOIN compras.OrdenCompra oc ON oc.IdOrdenCompra = c.IdOrdenCompra
INNER JOIN maestra.Moneda mon ON mon.IdMoneda = oc.IdMoneda
LEFT JOIN compras.OrdenCompraDetalle od
    ON od.IdOrdenCompra = oc.IdOrdenCompra
   AND od.IdMaterial = d.IdMaterial
LEFT JOIN maestra.Proveedor p
    ON p.IdProveedor = od.IdProveedor
WHERE d.IdCompra = @IdCompra
ORDER BY d.IdCompraDetalle;", new { IdCompra = idCompra });

            var documentos = await GetDocumentosAsync(idCompra);

            return new { compra, items, documentos };
        }

        public async Task<IEnumerable<dynamic>> GetDocumentosAsync(int idCompra)
        {
            using var db = Open();

            return await db.QueryAsync(@"
SELECT
    IdCompraDocumento,
    IdCompra,
    NombreArchivo,
    RutaArchivo,
    Extension,
    FechaCreacion
FROM compras.CompraDocumento
WHERE IdCompra = @IdCompra
ORDER BY IdCompraDocumento DESC;", new { IdCompra = idCompra });
        }



        private async Task<string> EnsureNumeroCompraAsync(IDbConnection db, IDbTransaction? tx, string numeroSolicitado)
        {
            if (!string.IsNullOrWhiteSpace(numeroSolicitado))
            {
                var existe = await db.ExecuteScalarAsync<int>(
                    "SELECT COUNT(1) FROM compras.Compra WHERE NumeroCompra = @NumeroCompra",
                    new { NumeroCompra = numeroSolicitado }, tx);

                if (existe == 0)
                    return numeroSolicitado;
            }

            var siguiente = await db.QuerySingleAsync<int>(new CommandDefinition(@"
SELECT ISNULL(MAX(TRY_CONVERT(INT, NumeroCompra)), 0) + 1
FROM compras.Compra;", transaction: tx));

            return siguiente.ToString();
        }
        public async Task SaveDocumentosAsync(int idCompra, IEnumerable<(string NombreArchivo, string RutaArchivo, string? Extension)> docs)
        {
            using var db = Open();

            foreach (var doc in docs)
            {
                var nombreArchivo = string.IsNullOrWhiteSpace(doc.NombreArchivo)
                    ? Path.GetFileName(doc.RutaArchivo ?? string.Empty)
                    : doc.NombreArchivo.Trim();

                var rutaArchivo = (doc.RutaArchivo ?? string.Empty).Trim();
                var extension = string.IsNullOrWhiteSpace(doc.Extension)
                    ? Path.GetExtension(nombreArchivo)
                    : doc.Extension.Trim();

                if (string.IsNullOrWhiteSpace(nombreArchivo))
                    throw new InvalidOperationException("No se pudo determinar el nombre del archivo PDF.");
                if (string.IsNullOrWhiteSpace(rutaArchivo))
                    throw new InvalidOperationException("No se pudo determinar la ruta del archivo PDF.");

                await db.ExecuteAsync(@"
INSERT INTO compras.CompraDocumento
(
    IdCompra,
    NombreArchivo,
    RutaArchivo,
    Extension,
    TipoDocumento,
    FechaCreacion
)
VALUES
(
    @IdCompra,
    @NombreArchivo,
    @RutaArchivo,
    @Extension,
    'Factura',
    GETDATE()
);", new
                {
                    IdCompra = idCompra,
                    NombreArchivo = nombreArchivo,
                    RutaArchivo = rutaArchivo,
                    Extension = extension
                });
            }
        }
    }
}
