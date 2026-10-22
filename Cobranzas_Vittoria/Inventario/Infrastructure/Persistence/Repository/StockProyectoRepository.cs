using System.Data;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Inventario.Domain.Model;
using Cobranzas_Vittoria.Inventario.Domain.Persistence;
using Cobranzas_Vittoria.Repositories;
using Dapper;

namespace Cobranzas_Vittoria.Inventario.Infrastructure.Persistence.Repository;

public sealed class StockProyectoRepository : RepositoryBase, IStockProyectoRepository
{
    public StockProyectoRepository(IDbConnectionFactory factory) : base(factory) { }

    public Task<IReadOnlyList<StockProyecto>> ListarStockAsync(FiltroStockProyecto filtro, CancellationToken ct = default)
        => ConsultarAsync<StockProyecto>("almacen.usp_StockProyecto_Listar", new
        {
            filtro.IdProyecto, filtro.IdEspecialidad, filtro.IdMaterial,
            FechaDesde = filtro.FechaDesde?.ToDateTime(TimeOnly.MinValue),
            FechaHasta = filtro.FechaHasta?.ToDateTime(TimeOnly.MinValue)
        }, ct);

    public Task<IReadOnlyList<TransferenciaProyecto>> ListarTransferenciasAsync(FiltroTransferenciaProyecto filtro, CancellationToken ct = default)
        => ConsultarAsync<TransferenciaProyecto>("almacen.usp_TransferenciaProyecto_Listar", new
        {
            filtro.IdTransferencia, filtro.IdProyectoOrigen, filtro.IdProyectoDestino,
            FechaDesde = filtro.FechaDesde?.ToDateTime(TimeOnly.MinValue),
            FechaHasta = filtro.FechaHasta?.ToDateTime(TimeOnly.MinValue)
        }, ct);

    public Task<IReadOnlyList<TransferenciaProyecto>> RegistrarTransferenciaAsync(RegistroTransferenciaProyecto registro, CancellationToken ct = default)
    {
        DataTable items = new();
        items.Columns.Add("IdMaterial", typeof(int));
        items.Columns.Add("Cantidad", typeof(decimal));
        foreach (ItemTransferenciaProyecto item in registro.Items)
        {
            items.Rows.Add(item.IdMaterial, item.Cantidad);
        }

        return ConsultarAsync<TransferenciaProyecto>("almacen.usp_TransferenciaProyecto_Registrar", new
        {
            registro.IdProyectoOrigen, registro.IdProyectoDestino,
            Fecha = registro.Fecha.ToDateTime(TimeOnly.MinValue),
            registro.Observacion,
            Items = items.AsTableValuedParameter("almacen.TVP_TransferenciaProyectoItem")
        }, ct);
    }

    private Task<IReadOnlyList<T>> ConsultarAsync<T>(string procedimiento, object parametros, CancellationToken ct)
        => TraductorErroresSql.EjecutarAsync<IReadOnlyList<T>>(async () =>
        {
            using IDbConnection db = Open();
            IEnumerable<T> filas = await db.QueryAsync<T>(new CommandDefinition(
                procedimiento, parametros, commandType: CommandType.StoredProcedure, cancellationToken: ct));
            return filas.AsList();
        }, "transferencia");
}
