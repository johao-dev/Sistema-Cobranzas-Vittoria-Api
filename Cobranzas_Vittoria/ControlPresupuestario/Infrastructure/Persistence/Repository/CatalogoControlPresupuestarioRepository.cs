using System.Data;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Repositories;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.ValueObject;
using Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Entity;
using Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Mapper;
using Dapper;

namespace Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Repository;

/// <summary>Adaptador Dapper de los catálogos auxiliares sobre sus SPs de listado.</summary>
public sealed class CatalogoControlPresupuestarioRepository : RepositoryBase, ICatalogoControlPresupuestarioRepository
{
    private const string Schema = "ControlPresupuestario.";

    public CatalogoControlPresupuestarioRepository(IDbConnectionFactory factory) : base(factory) { }

    public async Task<IReadOnlyList<EstadoPresupuesto>> ListarEstadosPresupuestoAsync(bool? activo)
        => (await ListarAsync<EstadoPresupuestoEntity>("usp_EstadoPresupuesto_Listar", activo))
            .Select(CatalogoControlPresupuestarioMapper.ToDomain).ToList();

    public async Task<IReadOnlyList<TipoCentroCosto>> ListarTiposCentroCostoAsync(bool? activo)
        => (await ListarAsync<TipoCentroCostoEntity>("usp_TipoCentroCosto_Listar", activo))
            .Select(CatalogoControlPresupuestarioMapper.ToDomain).ToList();

    public async Task<IReadOnlyList<TipoPartida>> ListarTiposPartidaAsync(bool? activo)
        => (await ListarAsync<TipoPartidaEntity>("usp_TipoPartida_Listar", activo))
            .Select(CatalogoControlPresupuestarioMapper.ToDomain).ToList();

    public async Task<IReadOnlyList<TipoMovimientoPresupuestal>> ListarTiposMovimientoAsync(bool? activo)
        => (await ListarAsync<TipoMovimientoPresupuestalEntity>("usp_TipoMovimientoPresupuestal_Listar", activo))
            .Select(CatalogoControlPresupuestarioMapper.ToDomain).ToList();

    public async Task<IReadOnlyList<Moneda>> ListarMonedasAsync(bool? activo)
        => (await ListarAsync<MonedaEntity>("usp_Moneda_Listar", activo))
            .Select(CatalogoControlPresupuestarioMapper.ToDomain).ToList();

    public async Task<IReadOnlyList<SeccionGasto>> ListarSeccionesGastoAsync(bool? activo)
        => (await ListarAsync<SeccionGastoEntity>("usp_SeccionGasto_Listar", activo))
            .Select(CatalogoControlPresupuestarioMapper.ToDomain).ToList();

    private async Task<IEnumerable<T>> ListarAsync<T>(string procedimiento, bool? activo)
    {
        using var db = Open();
        return await db.QueryAsync<T>(Schema + procedimiento, new { Activo = activo },
            commandType: CommandType.StoredProcedure);
    }
}
