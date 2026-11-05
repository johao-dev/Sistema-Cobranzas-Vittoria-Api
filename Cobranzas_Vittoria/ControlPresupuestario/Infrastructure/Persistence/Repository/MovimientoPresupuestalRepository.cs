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

    public async Task<IReadOnlyList<MovimientoPresupuestal>> ListarConsolidadosAsync(
        int idPresupuesto, int idCatalogoPartida)
    {
        using IDbConnection db = Open();
        IEnumerable<MovimientoPresupuestalEntity> filas = await db.QueryAsync<MovimientoPresupuestalEntity>("""
            SELECT mp.IdMovimientoPresupuestal, mp.IdPresupuestoDetalle, mp.IdTipoMovimientoPresupuestal,
                tm.Codigo AS TipoMovimiento, tm.Nombre AS NombreTipoMovimiento, mp.ClaveEvento,
                mp.Origen, mp.IdOrigen, mp.Afectacion, mp.Direccion, mp.Fecha, mp.Monto, mp.Observacion,
                v.IdPresupuesto, v.IdPresupuestoVersion, v.NumeroVersion, e.Codigo AS EstadoPresupuesto,
                d.IdCatalogoPartida, c.Codigo AS CodigoPartida, c.Nombre AS NombrePartida,
                p.Codigo AS CodigoPresupuesto, p.IdMoneda, m.Codigo AS CodigoMoneda, m.Simbolo AS SimboloMoneda
            FROM ControlPresupuestario.MovimientoPresupuestal mp
            JOIN ControlPresupuestario.TipoMovimientoPresupuestal tm
                ON tm.IdTipoMovimientoPresupuestal = mp.IdTipoMovimientoPresupuestal
            JOIN ControlPresupuestario.PresupuestoDetalle d ON d.IdPresupuestoDetalle = mp.IdPresupuestoDetalle
            JOIN ControlPresupuestario.CatalogoPartida c ON c.IdCatalogoPartida = d.IdCatalogoPartida
            JOIN ControlPresupuestario.PresupuestoVersion v ON v.IdPresupuestoVersion = d.IdPresupuestoVersion
            JOIN ControlPresupuestario.EstadoPresupuesto e ON e.IdEstadoPresupuesto = v.IdEstadoPresupuesto
            JOIN ControlPresupuestario.Presupuesto p ON p.IdPresupuesto = v.IdPresupuesto
            JOIN maestra.Moneda m ON m.IdMoneda = p.IdMoneda
            WHERE v.IdPresupuesto = @IdPresupuesto AND d.IdCatalogoPartida = @IdCatalogoPartida
              AND e.Codigo IN ('APROBADO', 'HISTORICO')
              AND (tm.Codigo IN ('COMPROMISO', 'LIBERACION', 'EJECUCION')
                  OR (tm.Codigo = 'AJUSTE' AND mp.Afectacion IN ('COMPROMISO', 'EJECUCION')
                      AND mp.Direccion IN ('INCREMENTO', 'DECREMENTO')))
              AND EXISTS (SELECT 1 FROM ControlPresupuestario.vw_ControlPresupuestarioVigente vigente
                  WHERE vigente.IdPresupuesto = v.IdPresupuesto AND vigente.IdCatalogoPartida = d.IdCatalogoPartida)
            ORDER BY mp.Fecha, mp.IdMovimientoPresupuestal;
            """, new { IdPresupuesto = idPresupuesto, IdCatalogoPartida = idCatalogoPartida });

        return filas.Select(MovimientoPresupuestalMapper.ToDomain).ToList();
    }

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
