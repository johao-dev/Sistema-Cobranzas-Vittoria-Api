using System.Data;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Dtos.Compras;
using Cobranzas_Vittoria.Entities;
using Cobranzas_Vittoria.Interfaces;
using Dapper;

namespace Cobranzas_Vittoria.Repositories
{
    public class OrdenCompraRepository : RepositoryBase, IOrdenCompraRepository
    {
        public OrdenCompraRepository(IDbConnectionFactory factory) : base(factory) { }

        public async Task<IEnumerable<OrdenCompra>> ListAsync(string? estado, int? idProveedor, int? idProyecto)
        {
            using var db = Open();

            const string sql = @"
SELECT
    oc.IdOrdenCompra,
    oc.NumeroOrdenCompra,
    oc.IdRequerimiento,
    r.NumeroRequerimiento,
    oc.IdProveedor,
    p.RazonSocial AS Proveedor,
    COALESCE(NULLIF(LTRIM(RTRIM(espAgg.Especialidades)), ''), NULLIF(LTRIM(RTRIM(e.Nombre)), ''), '-') AS Especialidad,
    COALESCE(NULLIF(LTRIM(RTRIM(espAgg.Especialidades)), ''), NULLIF(LTRIM(RTRIM(e.Nombre)), ''), '-') AS Especialidades,
    COALESCE(oc.IdProyecto, r.IdProyecto) AS IdProyecto,
    pr.NombreProyecto,
    oc.FechaOrdenCompra,
    oc.Descripcion,
    oc.Estado,
    oc.Total,
    oc.RutaPdf,
    oc.FechaCreacion,
    oc.IdUsuarioCreacion
FROM compras.OrdenCompra oc
LEFT JOIN compras.Requerimiento r ON r.IdRequerimiento = oc.IdRequerimiento
LEFT JOIN maestra.Proveedor p ON p.IdProveedor = oc.IdProveedor
LEFT JOIN maestra.Proyecto pr ON pr.IdProyecto = COALESCE(oc.IdProyecto, r.IdProyecto)
LEFT JOIN maestra.Especialidad e ON e.IdEspecialidad = r.IdEspecialidad
OUTER APPLY
(
    SELECT STRING_AGG(x.Nombre, ', ') AS Especialidades
    FROM
    (
        SELECT DISTINCT e2.Nombre
        FROM compras.OrdenCompraDetalle od
        INNER JOIN maestra.Material m2 ON m2.IdMaterial = od.IdMaterial
        INNER JOIN maestra.Especialidad e2 ON e2.IdEspecialidad = m2.IdEspecialidad
        WHERE od.IdOrdenCompra = oc.IdOrdenCompra
    ) x
) espAgg
WHERE (@Estado IS NULL OR @Estado = '' OR oc.Estado = @Estado)
  AND (@IdProveedor IS NULL OR oc.IdProveedor = @IdProveedor)
  AND (@IdProyecto IS NULL OR COALESCE(oc.IdProyecto, r.IdProyecto) = @IdProyecto)
ORDER BY oc.IdOrdenCompra DESC;";

            return await db.QueryAsync<OrdenCompra>(sql, new { Estado = estado, IdProveedor = idProveedor, IdProyecto = idProyecto });
        }

        public async Task<(OrdenCompra? head, List<OrdenCompraDetalle> items, List<OrdenCompraHistorial> historial)> GetAsync(int idOrdenCompra)
        {
            using var db = Open();
            using var multi = await db.QueryMultipleAsync(
                "compras.usp_OrdenCompra_Get",
                new { IdOrdenCompra = idOrdenCompra },
                commandType: CommandType.StoredProcedure
            );

            var head = await multi.ReadFirstOrDefaultAsync<OrdenCompra>();
            var items = (await multi.ReadAsync<OrdenCompraDetalle>()).AsList();
            var historial = (await multi.ReadAsync<OrdenCompraHistorial>()).AsList();

            if (head != null)
            {
                var meta = await db.QueryFirstOrDefaultAsync<dynamic>(@"
SELECT
    COALESCE(NULLIF(LTRIM(RTRIM(r.NumeroRequerimiento)), ''), '-') AS NumeroRequerimiento,
    COALESCE(NULLIF(LTRIM(RTRIM(espAgg.Especialidades)), ''), NULLIF(LTRIM(RTRIM(e.Nombre)), ''), '-') AS Especialidades
FROM compras.OrdenCompra oc
LEFT JOIN compras.Requerimiento r ON r.IdRequerimiento = oc.IdRequerimiento
LEFT JOIN maestra.Especialidad e ON e.IdEspecialidad = r.IdEspecialidad
OUTER APPLY
(
    SELECT STRING_AGG(x.Nombre, ', ') AS Especialidades
    FROM
    (
        SELECT DISTINCT e2.Nombre
        FROM compras.OrdenCompraDetalle od
        INNER JOIN maestra.Material m2 ON m2.IdMaterial = od.IdMaterial
        INNER JOIN maestra.Especialidad e2 ON e2.IdEspecialidad = m2.IdEspecialidad
        WHERE od.IdOrdenCompra = oc.IdOrdenCompra
    ) x
) espAgg
WHERE oc.IdOrdenCompra = @IdOrdenCompra;", new { IdOrdenCompra = idOrdenCompra });

                if (meta != null)
                {
                    head.NumeroRequerimiento = (string?)meta.NumeroRequerimiento;
                    head.Especialidades = (string?)meta.Especialidades;
                    head.Especialidad = (string?)meta.Especialidades;
                }

                var especialidadesDetalle = (await db.QueryAsync<dynamic>(@"
SELECT
    d.IdOrdenCompraDetalle,
    COALESCE(NULLIF(LTRIM(RTRIM(e.Nombre)), ''), '-') AS Especialidad
FROM compras.OrdenCompraDetalle d
INNER JOIN maestra.Material m ON m.IdMaterial = d.IdMaterial
LEFT JOIN maestra.Especialidad e ON e.IdEspecialidad = m.IdEspecialidad
WHERE d.IdOrdenCompra = @IdOrdenCompra;", new { IdOrdenCompra = idOrdenCompra })).ToDictionary(x => (int)x.IdOrdenCompraDetalle, x => (string?)x.Especialidad);

                foreach (var item in items)
                {
                    if (especialidadesDetalle.TryGetValue(item.IdOrdenCompraDetalle, out var esp))
                        item.Especialidad = esp;
                }
            }

            return (head, items, historial);
        }

        public async Task<(int IdOrdenCompra, decimal Total)> CrearAsync(OrdenCompraCreateDto dto)
        {
            using var db = Open();

            var numeroOrdenCompra = await EnsureNumeroOrdenCompraAsync(db, (dto.NumeroOrdenCompra ?? string.Empty).Trim());
            var fechaOrdenCompra = dto.FechaOrdenCompra == default ? DateTime.Today : dto.FechaOrdenCompra.Date;

            var tvp = new DataTable();
            tvp.Columns.Add("IdMaterial", typeof(int));
            tvp.Columns.Add("Cantidad", typeof(decimal));
            tvp.Columns.Add("IdProveedor", typeof(int));
            tvp.Columns.Add("PrecioUnitario", typeof(decimal));

            foreach (var it in dto.Items)
                tvp.Rows.Add(it.IdMaterial, it.Cantidad, it.IdProveedor, it.PrecioUnitario);

            var p = new DynamicParameters();
            p.Add("NumeroOrdenCompra", numeroOrdenCompra);
            p.Add("IdRequerimiento", dto.IdRequerimiento);
            p.Add("IdProveedor", dto.IdProveedor > 0 ? dto.IdProveedor : dto.Items.FirstOrDefault()?.IdProveedor);
            p.Add("IdProyecto", dto.IdProyecto);
            p.Add("FechaOrdenCompra", fechaOrdenCompra);
            p.Add("Descripcion", dto.Descripcion);
            p.Add("IdUsuarioCreacion", dto.IdUsuarioCreacion);
            p.Add("RutaPdf", dto.RutaPdf);
            p.Add("Items", tvp.AsTableValuedParameter("compras.TVP_OrdenCompraDetalle"));

            var res = await db.QueryFirstAsync<dynamic>(
                "compras.usp_OrdenCompra_CrearDesdeRequerimiento",
                p,
                commandType: CommandType.StoredProcedure
            );

            return ((int)res.IdOrdenCompra, (decimal)res.Total);
        }

        public async Task UpdateAsync(int idOrdenCompra, OrdenCompraUpdateDto dto)
        {
            using var db = Open();

            var numeroOrdenCompra = string.IsNullOrWhiteSpace(dto.NumeroOrdenCompra)
                ? await EnsureNumeroOrdenCompraAsync(db, string.Empty)
                : dto.NumeroOrdenCompra.Trim();
            var fechaOrdenCompra = dto.FechaOrdenCompra == default ? DateTime.Today : dto.FechaOrdenCompra.Date;

            var tvp = new DataTable();
            tvp.Columns.Add("IdMaterial", typeof(int));
            tvp.Columns.Add("Cantidad", typeof(decimal));
            tvp.Columns.Add("IdProveedor", typeof(int));
            tvp.Columns.Add("PrecioUnitario", typeof(decimal));

            foreach (var it in dto.Items)
                tvp.Rows.Add(it.IdMaterial, it.Cantidad, it.IdProveedor, it.PrecioUnitario);

            var p = new DynamicParameters();
            p.Add("IdOrdenCompra", idOrdenCompra);
            p.Add("NumeroOrdenCompra", numeroOrdenCompra);
            p.Add("IdRequerimiento", dto.IdRequerimiento);
            p.Add("IdProveedor", dto.IdProveedor > 0 ? dto.IdProveedor : dto.Items.FirstOrDefault()?.IdProveedor);
            p.Add("IdProyecto", dto.IdProyecto);
            p.Add("FechaOrdenCompra", fechaOrdenCompra);
            p.Add("Descripcion", dto.Descripcion);
            p.Add("IdUsuarioCreacion", dto.IdUsuarioCreacion);
            p.Add("RutaPdf", dto.RutaPdf);
            p.Add("Items", tvp.AsTableValuedParameter("compras.TVP_OrdenCompraDetalle"));

            await db.ExecuteAsync(
                "compras.usp_OrdenCompra_Actualizar",
                p,
                commandType: CommandType.StoredProcedure
            );
        }



        private async Task<string> EnsureNumeroOrdenCompraAsync(IDbConnection db, string numeroSolicitado)
        {
            if (!string.IsNullOrWhiteSpace(numeroSolicitado))
            {
                var existe = await db.ExecuteScalarAsync<int>(
                    "SELECT COUNT(1) FROM compras.OrdenCompra WHERE NumeroOrdenCompra = @NumeroOrdenCompra",
                    new { NumeroOrdenCompra = numeroSolicitado });

                if (existe == 0)
                    return numeroSolicitado;
            }

            var siguiente = await db.QuerySingleAsync<int>(@"
SELECT ISNULL(MAX(TRY_CONVERT(INT, NumeroOrdenCompra)), 0) + 1
FROM compras.OrdenCompra;");

            return siguiente.ToString();
        }
        public async Task UpdateEstadoAsync(int idOrdenCompra, string estadoNuevo, int? idUsuario, string? observacion)
        {
            using var db = Open();
            await db.ExecuteAsync(
                "compras.usp_OrdenCompra_ActualizarEstado",
                new { IdOrdenCompra = idOrdenCompra, EstadoNuevo = estadoNuevo, IdUsuario = idUsuario, Observacion = observacion },
                commandType: CommandType.StoredProcedure
            );
        }
    }
}
