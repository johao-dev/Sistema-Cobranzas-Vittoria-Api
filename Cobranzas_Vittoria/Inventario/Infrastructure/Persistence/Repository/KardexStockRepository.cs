using System.Data;
using Cobranzas_Vittoria.Inventario.Domain.Model;
using Cobranzas_Vittoria.Inventario.Domain.Persistence;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Inventario.Infrastructure.Persistence;
using Cobranzas_Vittoria.Repositories;
using Dapper;

namespace Cobranzas_Vittoria.Inventario.Infrastructure.Persistence.Repository;

/// <summary>Persistencia Dapper del Kardex; traduce los rechazos SQL del módulo.</summary>
public sealed class KardexStockRepository : RepositoryBase, IKardexStockRepository
{
    public KardexStockRepository(IDbConnectionFactory factory) : base(factory) { }

    public async Task<IReadOnlyList<StockKardex>> ListarAsync(
        FiltroStockKardex filtro,
        CancellationToken ct = default)
    {
        return await TraductorErroresSql.EjecutarAsync(async () =>
        {
            filtro ??= new FiltroStockKardex();

            using IDbConnection db = Open();
            // Dapper no soporta DateOnly como parametro de Stored Procedure en
            // Microsoft.Data.SqlClient 6.x: lo convertimos a DateTime para que
            // el driver lo serialice como DATE en SQL Server.
            IEnumerable<StockKardex> result = await db.QueryAsync<StockKardex>(
                new CommandDefinition(
                    commandText: "almacen.usp_Kardex_StockActual_Listar",
                    parameters: new
                    {
                        IdEspecialidad = filtro.IdEspecialidad,
                        FechaDesde = filtro.FechaDesde?.ToDateTime(TimeOnly.MinValue),
                        FechaHasta = filtro.FechaHasta?.ToDateTime(TimeOnly.MinValue)
                    },
                    cancellationToken: ct,
                    commandType: CommandType.StoredProcedure));
            return result.AsList();
        }, "entrada");
    }
}
