using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Interfaces;
using Dapper;

namespace Cobranzas_Vittoria.Repositories
{
    public class KardexRepository : RepositoryBase, IKardexRepository
    {
        public KardexRepository(IDbConnectionFactory factory) : base(factory) { }

        public async Task<IEnumerable<dynamic>> ListMovimientosAsync(int? idMaterial, int? idEspecialidad, string? fechaDesde, string? fechaHasta)
        {
            using var db = Open();

            var sql = @"
WITH ComprasBase AS
(
    SELECT
        c.IdCompra,
        c.NumeroCompra,
        c.FechaCompra,
        c.Observacion,
        cd.IdCompraDetalle,
        cd.IdMaterial,
        CAST(ISNULL(cd.Cantidad, 0) AS DECIMAL(18,2)) AS Entrada,
        CAST(0 AS DECIMAL(18,2)) AS Salida,
        m.Descripcion AS Material,
        m.IdEspecialidad,
        e.Nombre AS Especialidad
    FROM compras.Compra c
    INNER JOIN compras.CompraDetalle cd
        ON cd.IdCompra = c.IdCompra
    INNER JOIN maestra.Material m
        ON m.IdMaterial = cd.IdMaterial
    LEFT JOIN maestra.Especialidad e
        ON e.IdEspecialidad = m.IdEspecialidad
    WHERE (@IdMaterial IS NULL OR cd.IdMaterial = @IdMaterial)
      AND (@IdEspecialidad IS NULL OR m.IdEspecialidad = @IdEspecialidad)
      AND (@FechaDesde IS NULL OR c.FechaCompra >= CONVERT(date, @FechaDesde))
      AND (@FechaHasta IS NULL OR c.FechaCompra <= CONVERT(date, @FechaHasta))
),
KardexCompras AS
(
    SELECT
        cb.IdCompra,
        cb.IdCompraDetalle,
        cb.IdMaterial,
        cb.Material,
        cb.IdEspecialidad,
        cb.Especialidad,
        CAST(cb.FechaCompra AS datetime2(0)) AS FechaMovimiento,
        cb.Entrada,
        cb.Salida,
        SUM(cb.Entrada - cb.Salida) OVER (
            PARTITION BY cb.IdMaterial, cb.IdEspecialidad
            ORDER BY cb.FechaCompra, cb.IdCompra, cb.IdCompraDetalle
            ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
        ) AS Stock,
        CASE
            WHEN NULLIF(LTRIM(RTRIM(ISNULL(cb.Observacion, ''))), '') IS NOT NULL THEN cb.Observacion
            ELSE CONCAT('Ingreso por compra ', cb.NumeroCompra)
        END AS Observacion
    FROM ComprasBase cb
)
SELECT
    IdMaterial,
    IdEspecialidad,
    Especialidad,
    FechaMovimiento,
    Material,
    Entrada,
    Salida,
    Stock,
    Observacion,
    IdCompra
FROM KardexCompras
ORDER BY FechaMovimiento DESC, IdCompra DESC, IdCompraDetalle DESC;";

            return await db.QueryAsync(sql, new
            {
                IdMaterial = idMaterial,
                IdEspecialidad = idEspecialidad,
                FechaDesde = fechaDesde,
                FechaHasta = fechaHasta
            });
        }
    }
}
