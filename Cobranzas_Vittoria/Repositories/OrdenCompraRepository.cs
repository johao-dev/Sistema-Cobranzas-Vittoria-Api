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
            return await db.QueryAsync<OrdenCompra>(
                "compras.usp_OrdenCompra_List",
                new { Estado = estado, IdProveedor = idProveedor, IdProyecto = idProyecto },
                commandType: CommandType.StoredProcedure
            );
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
