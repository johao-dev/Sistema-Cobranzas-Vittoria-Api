using System.Data;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Repositories;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;
using Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Entity;
using Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Mapper;
using Dapper;

namespace Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Repository;

/// <summary>Adaptador Dapper de presupuestos sobre los SPs usp_Presupuesto_*.</summary>
public sealed class PresupuestoRepository : RepositoryBase, IPresupuestoRepository
{
    private const string Schema = "ControlPresupuestario.";

    public PresupuestoRepository(IDbConnectionFactory factory) : base(factory) { }

    public Task<IReadOnlyList<Presupuesto>> ListarAsync(bool? activo, int? idCentroCosto, int? idMoneda, string? busqueda)
        => TraductorErroresSql.EjecutarAsync<IReadOnlyList<Presupuesto>>(async () =>
    {
        using var db = Open();
        var filas = await db.QueryAsync<PresupuestoEntity>(Schema + "usp_Presupuesto_Listar",
            new { Activo = activo, IdCentroCosto = idCentroCosto, IdMoneda = idMoneda, Busqueda = busqueda },
            commandType: CommandType.StoredProcedure);
        return filas.Select(PresupuestoMapper.ToDomain).ToList();
    });

    public Task<Presupuesto?> ObtenerAsync(int idPresupuesto) => TraductorErroresSql.EjecutarAsync(async () =>
    {
        using var db = Open();
        var fila = await db.QueryFirstOrDefaultAsync<PresupuestoEntity>(Schema + "usp_Presupuesto_Obtener",
            new { IdPresupuesto = idPresupuesto }, commandType: CommandType.StoredProcedure);
        return fila is null ? null : PresupuestoMapper.ToDomain(fila);
    });

    public Task<PresupuestoCreado> CrearAsync(Presupuesto p, string usuario) => TraductorErroresSql.EjecutarAsync(async () =>
    {
        using var db = Open();
        var fila = await db.QueryFirstAsync<PresupuestoCreadoEntity>(Schema + "usp_Presupuesto_Crear",
            new
            {
                p.IdCentroCosto, p.IdMoneda, p.Codigo, p.Nombre, p.Descripcion, p.FechaInicio, p.FechaFin,
                UsuarioCreacion = usuario
            }, commandType: CommandType.StoredProcedure);
        return PresupuestoMapper.ToDomain(fila);
    });

    public async Task<RegistrosAsociadosPresupuesto> ContarRegistrosAsociadosAsync(int idPresupuesto)
    {
        using var db = Open();
        return await db.QuerySingleAsync<RegistrosAsociadosPresupuesto>("""
            SELECT
                (SELECT COUNT(*) FROM ControlPresupuestario.MovimientoPresupuestal m
                 JOIN ControlPresupuestario.PresupuestoDetalle d ON d.IdPresupuestoDetalle = m.IdPresupuestoDetalle
                 JOIN ControlPresupuestario.PresupuestoVersion v ON v.IdPresupuestoVersion = d.IdPresupuestoVersion
                 WHERE v.IdPresupuesto = @idPresupuesto) AS Movimientos,
                (SELECT COUNT(*) FROM contable.GastoDirecto g
                 JOIN ControlPresupuestario.PresupuestoDetalle d ON d.IdPresupuestoDetalle = g.IdPresupuestoDetalle
                 JOIN ControlPresupuestario.PresupuestoVersion v ON v.IdPresupuestoVersion = d.IdPresupuestoVersion
                 WHERE v.IdPresupuesto = @idPresupuesto AND g.Estado <> 'ANULADO') AS GastosDirectos,
                (SELECT COUNT(*) FROM compras.RequerimientoDetalle r
                 JOIN ControlPresupuestario.PresupuestoDetalle d ON d.IdPresupuestoDetalle = r.IdPresupuestoDetalle
                 JOIN ControlPresupuestario.PresupuestoVersion v ON v.IdPresupuestoVersion = d.IdPresupuestoVersion
                 WHERE v.IdPresupuesto = @idPresupuesto) AS LineasRequerimiento;
            """, new { idPresupuesto });
    }

    public Task ActualizarAsync(Presupuesto p) => TraductorErroresSql.EjecutarAsync(async () =>
    {
        using var db = Open();
        await db.ExecuteAsync(Schema + "usp_Presupuesto_Actualizar",
            new { p.IdPresupuesto, p.Nombre, p.Activo, p.Descripcion, p.FechaInicio, p.FechaFin },
            commandType: CommandType.StoredProcedure);
    });
}
