using System.Data;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Repositories;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model.Consultas;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;
using Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Entity;
using Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Mapper;
using Dapper;

namespace Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Repository;

/// <summary>
/// Adaptador de reporting: consume directamente las vistas existentes, sin SPs intermedios.
/// Los WHERE se arman con predicados parametrizados cuyos nombres de columna son literales del código.
/// </summary>
public sealed class ConsultaPresupuestariaRepository : RepositoryBase, IConsultaPresupuestariaRepository
{
    private const string Orden = " ORDER BY CodigoCentroCosto, CodigoPresupuesto, NumeroVersion, CodigoPartida;";

    public ConsultaPresupuestariaRepository(IDbConnectionFactory factory) : base(factory) { }

    public async Task<IReadOnlyList<PresupuestoResumenFila>> ResumenAsync(FiltroConsultaPresupuestaria filtro)
        => (await LeerAsync<PresupuestoResumenEntity>("vw_PresupuestoResumen", filtro, porPartida: true, conSaldo: true, Orden))
            .Select(ConsultaPresupuestariaMapper.ToDomain).ToList();

    public async Task<IReadOnlyList<PresupuestoVsComprometidoFila>> PresupuestoVsComprometidoAsync(FiltroConsultaPresupuestaria filtro)
        => (await LeerAsync<PresupuestoVsComprometidoEntity>("vw_PresupuestoVsComprometido", filtro, porPartida: true, conSaldo: false, Orden))
            .Select(ConsultaPresupuestariaMapper.ToDomain).ToList();

    public async Task<IReadOnlyList<PresupuestoVsEjecutadoFila>> PresupuestoVsEjecutadoAsync(FiltroConsultaPresupuestaria filtro)
        => (await LeerAsync<PresupuestoVsEjecutadoEntity>("vw_PresupuestoVsEjecutado", filtro, porPartida: true, conSaldo: false, Orden))
            .Select(ConsultaPresupuestariaMapper.ToDomain).ToList();

    public async Task<IReadOnlyList<SaldoPartidaFila>> SaldoAsync(FiltroConsultaPresupuestaria filtro)
        => (await LeerAsync<SaldoPresupuestarioEntity>("vw_Saldo", filtro, porPartida: true, conSaldo: true, Orden))
            .Select(ConsultaPresupuestariaMapper.ToDomain).ToList();

    public async Task<IReadOnlyList<GastoPorPartidaFila>> GastosPorPartidaAsync(FiltroConsultaPresupuestaria filtro)
        => (await LeerAsync<GastoPorPartidaEntity>("vw_GastosPorPartida", filtro, porPartida: true, conSaldo: false, Orden))
            .Select(ConsultaPresupuestariaMapper.ToDomain).ToList();

    public async Task<IReadOnlyList<GastoPorCentroCostoFila>> GastosPorCentroCostoAsync(FiltroConsultaPresupuestaria filtro)
        => (await LeerAsync<GastoPorCentroCostoEntity>("vw_GastosPorCentroCosto", filtro, porPartida: false, conSaldo: true,
                " ORDER BY CodigoCentroCosto, CodigoPresupuesto, NumeroVersion;"))
            .Select(ConsultaPresupuestariaMapper.ToDomain).ToList();

    public async Task<IReadOnlyList<SaldoPartidaFila>> VigenteAsync(FiltroConsultaPresupuestaria filtro)
        => (await LeerAsync<ControlPresupuestarioVigenteEntity>("vw_ControlPresupuestarioVigente", filtro, porPartida: true,
                conSaldo: true, Orden))
            .Select(ConsultaPresupuestariaMapper.ToDomain).ToList();

    public async Task<IReadOnlyList<EjecucionDiariaFila>> EjecucionDiariaAsync(IReadOnlyCollection<int> idsPresupuesto)
    {
        if (idsPresupuesto.Count == 0) return Array.Empty<EjecucionDiariaFila>();
        using var db = Open();
        var filas = await db.QueryAsync<EjecucionDiariaEntity>("""
            SELECT IdPresupuesto, IdCatalogoPartida, Fecha, MontoEjecutado
            FROM ControlPresupuestario.vw_EjecucionDiariaPorPartida
            WHERE IdPresupuesto IN @Ids
            ORDER BY Fecha;
            """, new { Ids = idsPresupuesto });
        return filas.Select(ConsultaPresupuestariaMapper.ToDomain).ToList();
    }

    public async Task<SaldoPartidaFila?> SaldoDetalleAsync(int idPresupuestoDetalle)
    {
        using var db = Open();
        var fila = await db.QueryFirstOrDefaultAsync<SaldoPresupuestarioEntity>(
            "SELECT TOP (1) * FROM ControlPresupuestario.vw_Saldo WHERE IdPresupuestoDetalle = @IdPresupuestoDetalle;",
            new { IdPresupuestoDetalle = idPresupuestoDetalle });
        return fila is null ? null : ConsultaPresupuestariaMapper.ToDomain(fila);
    }

    private async Task<IEnumerable<T>> LeerAsync<T>(string vista, FiltroConsultaPresupuestaria filtro, bool porPartida,
        bool conSaldo, string orden)
    {
        var (where, parametros) = ConstruirFiltro(filtro, porPartida, conSaldo);
        using var db = Open();
        return await db.QueryAsync<T>($"SELECT * FROM ControlPresupuestario.{vista}{where}{orden}", parametros);
    }

    private static (string Where, DynamicParameters Parametros) ConstruirFiltro(FiltroConsultaPresupuestaria f,
        bool porPartida, bool conSaldo)
    {
        var condiciones = new List<string>();
        var parametros = new DynamicParameters();
        void Agregar(string columna, object? valor)
        {
            if (valor is null) return;
            condiciones.Add($"{columna} = @{columna}");
            parametros.Add(columna, valor);
        }

        Agregar("IdCentroCosto", f.IdCentroCosto);
        Agregar("IdPresupuesto", f.IdPresupuesto);
        Agregar("IdPresupuestoVersion", f.IdPresupuestoVersion);
        Agregar("IdMoneda", f.IdMoneda);
        if (porPartida) Agregar("IdCatalogoPartida", f.IdCatalogoPartida);
        Agregar("EstadoPresupuesto", f.EstadoPresupuesto);
        if (f.SoloExcedidos == true && conSaldo) condiciones.Add("SaldoDisponible < 0");
        if (f.SoloPresupuestosActivos)
            condiciones.Add("IdPresupuesto IN (SELECT IdPresupuesto FROM ControlPresupuestario.Presupuesto WHERE Activo = 1)");

        return (condiciones.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", condiciones), parametros);
    }
}
