using System.Data;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Repositories;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.ValueObject;
using Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Entity;
using Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Mapper;
using Dapper;

namespace Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Repository;

/// <summary>
/// Adaptador Dapper del ledger. Solo lee; la única escritura es el ajuste manual, que usa el mismo
/// mecanismo interno (usp_MovimientoPresupuestal_Registrar) que las integraciones económicas.
/// </summary>
public sealed class MovimientoPresupuestalRepository : RepositoryBase, IMovimientoPresupuestalRepository
{
    private const string Schema = "ControlPresupuestario.";

    public MovimientoPresupuestalRepository(IDbConnectionFactory factory) : base(factory) { }

    public Task<IReadOnlyList<MovimientoPresupuestal>> ListarPorDetalleAsync(int idPresupuestoDetalle)
        => TraductorErroresSql.EjecutarAsync<IReadOnlyList<MovimientoPresupuestal>>(async () =>
    {
        using var db = Open();
        var filas = await db.QueryAsync<MovimientoPresupuestalEntity>(Schema + "usp_MovimientoPresupuestal_ListarPorDetalle",
            new { IdPresupuestoDetalle = idPresupuestoDetalle }, commandType: CommandType.StoredProcedure);
        return filas.Select(MovimientoPresupuestalMapper.ToDomain).ToList();
    });

    public Task<MovimientoPresupuestal?> ObtenerAsync(long idMovimientoPresupuestal) => TraductorErroresSql.EjecutarAsync(async () =>
    {
        using var db = Open();
        var fila = await db.QueryFirstOrDefaultAsync<MovimientoPresupuestalEntity>(Schema + "usp_MovimientoPresupuestal_Obtener",
            new { IdMovimientoPresupuestal = idMovimientoPresupuestal }, commandType: CommandType.StoredProcedure);
        return fila is null ? null : MovimientoPresupuestalMapper.ToDomain(fila);
    });

    public Task<MovimientoPresupuestal?> RegistrarAjusteAsync(AjustePresupuestal a) => TraductorErroresSql.EjecutarAsync(async () =>
    {
        using var db = Open();
        var fila = await db.QueryFirstOrDefaultAsync<MovimientoPresupuestalEntity>(Schema + "usp_MovimientoPresupuestal_Registrar",
            new
            {
                a.IdPresupuestoDetalle,
                TipoMovimiento = TipoMovimientoPresupuestal.Ajuste,
                a.ClaveEvento,
                a.Origen,
                a.IdOrigen,
                a.Monto,
                a.Fecha,
                a.Observacion,
                Afectacion = a.Afectacion.Valor,
                Direccion = a.Direccion.Valor
            }, commandType: CommandType.StoredProcedure);
        return fila is null ? null : MovimientoPresupuestalMapper.ToDomain(fila);
    });
}
