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
    CAST('Comprado' AS NVARCHAR(20)) AS Estado,
    c.IncluyeIGV,
    c.SubtotalSinIGV,
    c.MontoIGV,
    c.MontoTotal,
    c.Observacion,
    c.IdOrdenCompra,
    c.IdProveedor,
    oc.NumeroOrdenCompra,
    p.RazonSocial AS Proveedor,
    COALESCE(NULLIF(LTRIM(RTRIM(r.NumeroRequerimiento)), ''), '-') AS NumeroRequerimiento,
    COALESCE(NULLIF(LTRIM(RTRIM(pr.NombreProyecto)), ''), '-') AS NombreProyecto,
    COALESCE(NULLIF(LTRIM(RTRIM(espAgg.Especialidad)), ''), NULLIF(LTRIM(RTRIM(e.Nombre)), ''), '-') AS Especialidad
FROM compras.Compra c
INNER JOIN compras.OrdenCompra oc ON oc.IdOrdenCompra = c.IdOrdenCompra
LEFT JOIN compras.Requerimiento r ON r.IdRequerimiento = oc.IdRequerimiento
LEFT JOIN maestra.Proveedor p ON p.IdProveedor = c.IdProveedor
LEFT JOIN maestra.Especialidad e ON e.IdEspecialidad = r.IdEspecialidad
LEFT JOIN maestra.Proyecto pr ON pr.IdProyecto = COALESCE(oc.IdProyecto, r.IdProyecto)
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
  AND (@IdProveedor IS NULL OR c.IdProveedor = @IdProveedor)
ORDER BY c.IdCompra DESC;";

            return await db.QueryAsync(sql, new { Aceptada = aceptada, IdProveedor = idProveedor });
        }

        public async Task<(int IdCompra, decimal MontoTotal)> CrearAsync(CompraCreateDto dto)
        {
            using var db = Open();
            using var tx = db.BeginTransaction();

            var numeroCompra = await EnsureNumeroCompraAsync(db, tx, (dto.NumeroCompra ?? string.Empty).Trim());
            var fechaCompra = dto.FechaCompra == default ? DateTime.Today : dto.FechaCompra.Date;

            var montoBaseConIgv = (dto.Items ?? new List<CompraDetalleCreateDto>())
                .Sum(x => x.Cantidad * x.PrecioUnitario);

            decimal subtotalSinIgv;
            decimal montoIgv;
            decimal montoTotal;

            if (dto.IncluyeIGV)
            {
                montoTotal = Math.Round(montoBaseConIgv, 2);
                subtotalSinIgv = Math.Round(montoTotal / 1.18m, 2);
                montoIgv = Math.Round(montoTotal - subtotalSinIgv, 2);
            }
            else
            {
                subtotalSinIgv = Math.Round(montoBaseConIgv, 2);
                montoIgv = 0;
                montoTotal = subtotalSinIgv;
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
                NumeroCompra = numeroCompra,
                dto.IdOrdenCompra,
                dto.IdProveedor,
                FechaCompra = fechaCompra,
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
    CAST('Comprado' AS NVARCHAR(20)) AS Estado,
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
    ISNULL(od.IdProveedor, oc.IdProveedor) AS IdProveedor,
    p.RazonSocial AS Proveedor
FROM compras.CompraDetalle d
INNER JOIN maestra.Material m ON m.IdMaterial = d.IdMaterial
INNER JOIN compras.Compra c ON c.IdCompra = d.IdCompra
INNER JOIN compras.OrdenCompra oc ON oc.IdOrdenCompra = c.IdOrdenCompra
LEFT JOIN compras.OrdenCompraDetalle od
    ON od.IdOrdenCompra = oc.IdOrdenCompra
   AND od.IdMaterial = d.IdMaterial
LEFT JOIN maestra.Proveedor p
    ON p.IdProveedor = ISNULL(od.IdProveedor, oc.IdProveedor)
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



        private async Task<string> EnsureNumeroCompraAsync(IDbConnection db, IDbTransaction tx, string numeroSolicitado)
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
