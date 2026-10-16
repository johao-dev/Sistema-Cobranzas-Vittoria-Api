using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Dtos.Cotizaciones;
using Cobranzas_Vittoria.Entities;
using Cobranzas_Vittoria.Interfaces;
using Dapper;
using System.Data;

namespace Cobranzas_Vittoria.Repositories
{
    public class CotizacionesRepository : ICotizacionesRepository
    {
        private readonly IDbConnectionFactory _factory;

        public CotizacionesRepository(IDbConnectionFactory factory)
        {
            _factory = factory;
        }

        public async Task<int> CrearAsync(int idRequerimiento, CotizacionCreateDto dto)
        {
            using var db = _factory.CreateConnection();

            // TVP dbo.TVP_CotizacionDetalle: (IdMaterial, Cantidad, PrecioUnitario)
            var tvp = new DataTable();
            tvp.Columns.Add("IdMaterial", typeof(int));
            tvp.Columns.Add("Cantidad", typeof(decimal));
            tvp.Columns.Add("PrecioUnitario", typeof(decimal));

            foreach (var it in dto.Items)
            {
                tvp.Rows.Add(it.IdMaterial, it.Cantidad, it.PrecioUnitario);
            }

            var p = new DynamicParameters();
            p.Add("IdRequerimiento", idRequerimiento);
            p.Add("IdProveedor", dto.IdProveedor);
            p.Add("Items", tvp.AsTableValuedParameter("dbo.TVP_CotizacionDetalle"));

            return await db.ExecuteScalarAsync<int>(
                "dbo.usp_Cotizacion_Crear",
                p,
                commandType: CommandType.StoredProcedure
            );
        }

        public async Task<(Cotizacion? cotizacion, List<CotizacionDetalle> items)> ObtenerAsync(int idCotizacion)
        {
            using var db = _factory.CreateConnection();

            using var grid = await db.QueryMultipleAsync(
                "dbo.usp_Cotizacion_Get",
                new { IdCotizacion = idCotizacion },
                commandType: CommandType.StoredProcedure
            );

            var head = await grid.ReadFirstOrDefaultAsync<Cotizacion>();
            var items = (await grid.ReadAsync<CotizacionDetalle>()).AsList();

            return (head, items);
        }
    }
}
