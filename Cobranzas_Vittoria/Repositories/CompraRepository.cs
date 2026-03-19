using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Dtos.Compras;
using Cobranzas_Vittoria.Interfaces;
using Dapper;
using System.Data;
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
    CASE WHEN c.Aceptada = 1 THEN 'Aceptada' ELSE 'Pendiente' END AS Estado,
    c.IncluyeIGV,
    c.SubtotalSinIGV,
    c.MontoIGV,
    c.MontoTotal,
    c.Observacion,
    p.RazonSocial AS Proveedor,
    COALESCE(NULLIF(LTRIM(RTRIM(r.NumeroRequerimiento)), ''), '-') AS NumeroRequerimiento,
    COALESCE(NULLIF(LTRIM(RTRIM(pr.NombreProyecto)), ''), '-') AS NombreProyecto,
    COALESCE(NULLIF(LTRIM(RTRIM(espAgg.Especialidad)), ''), NULLIF(LTRIM(RTRIM(e.Nombre)), ''), '-') AS Especialidad,
    oc.NumeroOrdenCompra
FROM compras.Compra c
INNER JOIN compras.OrdenCompra oc ON oc.IdOrdenCompra = c.IdOrdenCompra
LEFT JOIN compras.Requerimiento r ON r.IdRequerimiento = oc.IdRequerimiento
LEFT JOIN maestra.Proveedor p ON p.IdProveedor = c.IdProveedor
LEFT JOIN maestra.Especialidad e ON e.IdEspecialidad = r.IdEspecialidad
LEFT JOIN maestra.Proyecto pr ON pr.IdProyecto = COALESCE(oc.IdProyecto, r.IdProyecto)
OUTER APPLY (
    SELECT STRING_AGG(x.Nombre, ', ') AS Especialidad
    FROM (
        SELECT DISTINCT e2.Nombre
        FROM compras.OrdenCompraDetalle od
        INNER JOIN maestra.Material m ON m.IdMaterial = od.IdMaterial
        INNER JOIN maestra.Especialidad e2 ON e2.IdEspecialidad = m.IdEspecialidad
        WHERE od.IdOrdenCompra = oc.IdOrdenCompra
    ) x
) espAgg
WHERE (@Aceptada IS NULL OR c.Aceptada = @Aceptada)
  AND (@IdProveedor IS NULL OR c.IdProveedor = @IdProveedor)
ORDER BY c.IdCompra DESC";

            return await db.QueryAsync(sql, new { Aceptada = aceptada, IdProveedor = idProveedor });
        }

        public async Task<(int IdCompra, decimal MontoTotal)> CrearAsync(CompraCreateDto dto)
        {
            using var db = Open();
            using var tx = db.BeginTransaction();

            var subtotalSinIgv = dto.SubtotalSinIGV;
            var montoIgv = dto.MontoIGV;
            var montoTotal = dto.MontoTotal;

            if (dto.Items != null && dto.Items.Count > 0)
            {
                var calculado = dto.Items.Sum(x => x.Cantidad * x.PrecioUnitario);
                if (subtotalSinIgv <= 0 && montoTotal > 0 && dto.IncluyeIGV)
                {
                    subtotalSinIgv = Math.Round(montoTotal / 1.18m, 2);
                    montoIgv = Math.Round(montoTotal - subtotalSinIgv, 2);
                }
                else if (subtotalSinIgv <= 0)
                {
                    subtotalSinIgv = Math.Round(calculado, 2);
                }

                if (dto.IncluyeIGV)
                {
                    if (montoTotal <= 0)
                        montoTotal = Math.Round(subtotalSinIgv + montoIgv, 2);
                    if (montoIgv <= 0)
                        montoIgv = Math.Round(montoTotal - subtotalSinIgv, 2);
                }
                else
                {
                    montoIgv = 0;
                    if (montoTotal <= 0)
                        montoTotal = Math.Round(subtotalSinIgv, 2);
                }
            }

            const string sqlCompra = @"
INSERT INTO compras.Compra
(
    NumeroCompra,
    IdOrdenCompra,
    IdProveedor,
    FechaCompra,
    Aceptada,
    IncluyeIGV,
    SubtotalSinIGV,
    MontoIGV,
    MontoTotal,
    Observacion,
    FechaCreacion
)
VALUES
(
    @NumeroCompra,
    @IdOrdenCompra,
    @IdProveedor,
    @FechaCompra,
    0,
    @IncluyeIGV,
    @SubtotalSinIGV,
    @MontoIGV,
    @MontoTotal,
    @Observacion,
    GETDATE()
);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var idCompra = await db.ExecuteScalarAsync<int>(sqlCompra, new
            {
                dto.NumeroCompra,
                dto.IdOrdenCompra,
                dto.IdProveedor,
                FechaCompra = dto.FechaCompra.Date,
                IncluyeIGV = dto.IncluyeIGV,
                SubtotalSinIGV = subtotalSinIgv,
                MontoIGV = montoIgv,
                MontoTotal = montoTotal,
                dto.Observacion
            }, tx);

            if (dto.Items != null && dto.Items.Count > 0)
            {
                const string sqlDetalle = @"
INSERT INTO compras.CompraDetalle (IdCompra, IdMaterial, Cantidad, PrecioUnitario)
VALUES (@IdCompra, @IdMaterial, @Cantidad, @PrecioUnitario);";

                foreach (var item in dto.Items)
                {
                    await db.ExecuteAsync(sqlDetalle, new
                    {
                        IdCompra = idCompra,
                        item.IdMaterial,
                        item.Cantidad,
                        item.PrecioUnitario
                    }, tx);
                }
            }

            tx.Commit();
            return (idCompra, montoTotal);
        }

        public async Task<IEnumerable<dynamic>> ListPendientesDesdeOcAsync()
        {
            using var db = Open();

            var sql = @"
SELECT
    oc.IdOrdenCompra,
    oc.NumeroOrdenCompra,
    oc.FechaOrdenCompra,
    oc.Estado,
    oc.Total,
    oc.IdProveedor,
    p.RazonSocial AS Proveedor,
    r.IdRequerimiento,
    COALESCE(NULLIF(LTRIM(RTRIM(r.NumeroRequerimiento)), ''), '-') AS NumeroRequerimiento,
    COALESCE(NULLIF(LTRIM(RTRIM(espAgg.Especialidad)), ''), NULLIF(LTRIM(RTRIM(e.Nombre)), ''), '-') AS Especialidad,
    COALESCE(NULLIF(LTRIM(RTRIM(pr.NombreProyecto)), ''), '-') AS NombreProyecto
FROM compras.OrdenCompra oc
LEFT JOIN compras.Requerimiento r ON r.IdRequerimiento = oc.IdRequerimiento
LEFT JOIN maestra.Proveedor p ON p.IdProveedor = oc.IdProveedor
LEFT JOIN maestra.Especialidad e ON e.IdEspecialidad = r.IdEspecialidad
LEFT JOIN maestra.Proyecto pr ON pr.IdProyecto = COALESCE(oc.IdProyecto, r.IdProyecto)
OUTER APPLY (
    SELECT STRING_AGG(x.Nombre, ', ') AS Especialidad
    FROM (
        SELECT DISTINCT e2.Nombre
        FROM compras.OrdenCompraDetalle od
        INNER JOIN maestra.Material m ON m.IdMaterial = od.IdMaterial
        INNER JOIN maestra.Especialidad e2 ON e2.IdEspecialidad = m.IdEspecialidad
        WHERE od.IdOrdenCompra = oc.IdOrdenCompra
    ) x
) espAgg
LEFT JOIN compras.Compra c ON c.IdOrdenCompra = oc.IdOrdenCompra
WHERE c.IdCompra IS NULL
ORDER BY oc.IdOrdenCompra DESC";

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
    CASE WHEN c.Aceptada = 1 THEN 'Aceptada' ELSE 'Pendiente' END AS Estado,
    c.IncluyeIGV,
    c.SubtotalSinIGV,
    c.MontoIGV,
    c.MontoTotal,
    c.Observacion,
    c.IdOrdenCompra,
    oc.NumeroOrdenCompra,
    COALESCE(NULLIF(LTRIM(RTRIM(r.NumeroRequerimiento)), ''), '-') AS NumeroRequerimiento,
    p.RazonSocial AS Proveedor,
    COALESCE(NULLIF(LTRIM(RTRIM(espAgg.Especialidad)), ''), NULLIF(LTRIM(RTRIM(e.Nombre)), ''), '-') AS Especialidad,
    COALESCE(NULLIF(LTRIM(RTRIM(pr.NombreProyecto)), ''), '-') AS NombreProyecto
FROM compras.Compra c
INNER JOIN compras.OrdenCompra oc ON oc.IdOrdenCompra = c.IdOrdenCompra
LEFT JOIN compras.Requerimiento r ON r.IdRequerimiento = oc.IdRequerimiento
LEFT JOIN maestra.Proveedor p ON p.IdProveedor = c.IdProveedor
LEFT JOIN maestra.Especialidad e ON e.IdEspecialidad = r.IdEspecialidad
LEFT JOIN maestra.Proyecto pr ON pr.IdProyecto = COALESCE(oc.IdProyecto, r.IdProyecto)
OUTER APPLY (
    SELECT STRING_AGG(x.Nombre, ', ') AS Especialidad
    FROM (
        SELECT DISTINCT e2.Nombre
        FROM compras.OrdenCompraDetalle od
        INNER JOIN maestra.Material m ON m.IdMaterial = od.IdMaterial
        INNER JOIN maestra.Especialidad e2 ON e2.IdEspecialidad = m.IdEspecialidad
        WHERE od.IdOrdenCompra = oc.IdOrdenCompra
    ) x
) espAgg
WHERE c.IdCompra = @IdCompra", new { IdCompra = idCompra });

            if (compra == null) return null;

            var items = await db.QueryAsync(@"
SELECT
    d.IdCompraDetalle,
    d.IdCompra,
    d.IdMaterial,
    m.Descripcion AS Material,
    m.UnidadMedida,
    d.Cantidad,
    d.PrecioUnitario,
    d.Subtotal
FROM compras.CompraDetalle d
INNER JOIN maestra.Material m ON m.IdMaterial = d.IdMaterial
WHERE d.IdCompra = @IdCompra
ORDER BY d.IdCompraDetalle", new { IdCompra = idCompra });

            var documentos = await GetDocumentosAsync(idCompra);

            return new { compra, items, documentos };
        }

        public async Task<IEnumerable<dynamic>> GetDocumentosAsync(int idCompra)
        {
            using var db = Open();
            var cols = (await db.QueryAsync<string>(@"
SELECT COLUMN_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'compras'
  AND TABLE_NAME = 'CompraDocumento'
ORDER BY ORDINAL_POSITION")).ToList();

            string Pick(params string[] names) =>
                cols.FirstOrDefault(c => names.Any(n => string.Equals(n, c, StringComparison.OrdinalIgnoreCase))) ?? string.Empty;

            var colId = Pick("IdCompraDocumento");
            var colIdCompra = Pick("IdCompra");
            var colNombre = Pick("NombreArchivo", "NombreDocumento", "Nombre", "Archivo", "NumeroDocumento");
            var colRuta = Pick("RutaArchivo", "RutaDocumento", "Ruta", "UrlArchivo", "ArchivoRuta", "PathArchivo");
            var colExt = Pick("Extension", "Ext", "TipoArchivo", "Tipo");
            var colFecha = Pick("FechaCreacion", "FechaRegistro", "Fecha", "FechaCarga", "FechaDocumento");
            var colTipoDocumento = Pick("TipoDocumento");

            if (string.IsNullOrWhiteSpace(colIdCompra))
                return Enumerable.Empty<dynamic>();

            var selectId = string.IsNullOrWhiteSpace(colId) ? "NULL AS IdCompraDocumento" : $"[{colId}] AS IdCompraDocumento";
            var selectNombre = string.IsNullOrWhiteSpace(colNombre) ? "NULL AS NombreArchivo" : $"[{colNombre}] AS NombreArchivo";
            var selectRuta = string.IsNullOrWhiteSpace(colRuta) ? "NULL AS RutaArchivo" : $"[{colRuta}] AS RutaArchivo";
            var selectExt = string.IsNullOrWhiteSpace(colExt) ? "NULL AS Extension" : $"[{colExt}] AS Extension";
            var selectFecha = string.IsNullOrWhiteSpace(colFecha) ? "NULL AS FechaCreacion" : $"[{colFecha}] AS FechaCreacion";
            var selectTipoDoc = string.IsNullOrWhiteSpace(colTipoDocumento) ? "NULL AS TipoDocumento" : $"[{colTipoDocumento}] AS TipoDocumento";

            var sql = $@"
SELECT
    {selectId},
    [{colIdCompra}] AS IdCompra,
    {selectNombre},
    {selectRuta},
    {selectExt},
    {selectFecha},
    {selectTipoDoc}
FROM compras.CompraDocumento
WHERE [{colIdCompra}] = @IdCompra
ORDER BY 1 DESC";

            return await db.QueryAsync(sql, new { IdCompra = idCompra });
        }

        public async Task SaveDocumentosAsync(int idCompra, IEnumerable<(string NombreArchivo, string RutaArchivo, string? Extension)> docs)
        {
            using var db = Open();
            var meta = (await db.QueryAsync(@"
SELECT
    COLUMN_NAME AS ColumnName,
    IS_NULLABLE AS IsNullable
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'compras'
  AND TABLE_NAME = 'CompraDocumento'
ORDER BY ORDINAL_POSITION")).ToList();

            var cols = meta.Select(x => (string)x.ColumnName).ToList();

            string Pick(params string[] names) =>
                cols.FirstOrDefault(c => names.Any(n => string.Equals(n, c, StringComparison.OrdinalIgnoreCase))) ?? string.Empty;

            var colIdCompra = Pick("IdCompra");
            var colNombre = Pick("NombreArchivo", "NombreDocumento", "Nombre", "Archivo", "NumeroDocumento");
            var colRuta = Pick("RutaArchivo", "RutaDocumento", "Ruta", "UrlArchivo", "ArchivoRuta", "PathArchivo");
            var colExt = Pick("Extension", "Ext", "TipoArchivo", "Tipo");
            var colFecha = Pick("FechaCreacion", "FechaRegistro", "Fecha", "FechaCarga", "FechaDocumento");
            var colTipoDocumento = Pick("TipoDocumento");
            var colMonto = Pick("Monto");
            var colObs = Pick("Observacion");

            if (string.IsNullOrWhiteSpace(colIdCompra) || string.IsNullOrWhiteSpace(colNombre) || string.IsNullOrWhiteSpace(colRuta))
                throw new InvalidOperationException("La tabla compras.CompraDocumento no tiene las columnas mínimas requeridas para guardar documentos.");

            foreach (var doc in docs)
            {
                var columns = new List<string> { $"[{colIdCompra}]", $"[{colNombre}]", $"[{colRuta}]" };
                var values = new List<string> { "@IdCompra", "@NombreArchivo", "@RutaArchivo" };

                if (!string.IsNullOrWhiteSpace(colExt))
                {
                    columns.Add($"[{colExt}]");
                    values.Add("@Extension");
                }

                if (!string.IsNullOrWhiteSpace(colTipoDocumento))
                {
                    columns.Add($"[{colTipoDocumento}]");
                    values.Add("@TipoDocumento");
                }

                if (!string.IsNullOrWhiteSpace(colFecha))
                {
                    columns.Add($"[{colFecha}]");
                    values.Add("GETDATE()");
                }

                if (!string.IsNullOrWhiteSpace(colMonto))
                {
                    columns.Add($"[{colMonto}]");
                    values.Add("0");
                }

                if (!string.IsNullOrWhiteSpace(colObs))
                {
                    columns.Add($"[{colObs}]");
                    values.Add("NULL");
                }

                var sql = $@"
INSERT INTO compras.CompraDocumento
(
    {string.Join(", ", columns)}
)
VALUES
(
    {string.Join(", ", values)}
)";

                await db.ExecuteAsync(sql, new
                {
                    IdCompra = idCompra,
                    NombreArchivo = doc.NombreArchivo,
                    RutaArchivo = doc.RutaArchivo,
                    Extension = doc.Extension,
                    TipoDocumento = "Factura"
                });
            }
        }
    }
}
