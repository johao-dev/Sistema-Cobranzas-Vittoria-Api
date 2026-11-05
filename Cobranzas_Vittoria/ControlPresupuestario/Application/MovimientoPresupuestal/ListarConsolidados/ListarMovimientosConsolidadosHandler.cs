using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model.Consultas;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.MovimientoPresupuestal.ListarConsolidados;

public sealed record ListarMovimientosConsolidadosQuery(int IdPresupuesto, int IdCatalogoPartida);

/// <summary>Traza los movimientos que explican una fila de los reportes consolidados.</summary>
public sealed class ListarMovimientosConsolidadosHandler
{
    private readonly IPresupuestoRepository _presupuestos;
    private readonly IConsultaPresupuestariaRepository _consultas;
    private readonly IMovimientoPresupuestalRepository _movimientos;

    public ListarMovimientosConsolidadosHandler(IPresupuestoRepository presupuestos,
        IConsultaPresupuestariaRepository consultas, IMovimientoPresupuestalRepository movimientos)
    {
        _presupuestos = presupuestos;
        _consultas = consultas;
        _movimientos = movimientos;
    }

    public async Task<IReadOnlyList<MovimientoConsolidadoResult>> HandleAsync(ListarMovimientosConsolidadosQuery query)
    {
        if (query.IdPresupuesto <= 0)
            throw ValidacionPresupuestariaException.IdentificadorInvalido(nameof(query.IdPresupuesto));
        if (query.IdCatalogoPartida <= 0)
            throw ValidacionPresupuestariaException.IdentificadorInvalido(nameof(query.IdCatalogoPartida));

        _ = await _presupuestos.ObtenerAsync(query.IdPresupuesto)
            ?? throw new PresupuestoNoEncontradoException(query.IdPresupuesto);

        // También permite inspeccionar presupuestos inactivos: es una lectura de trazabilidad.
        IReadOnlyList<SaldoPartidaFila> partidas = await _consultas.VigenteAsync(new FiltroConsultaPresupuestaria(
            IdPresupuesto: query.IdPresupuesto, IdCatalogoPartida: query.IdCatalogoPartida));
        if (partidas.Count == 0)
            throw new RecursoPresupuestarioNoEncontradoException("PARTIDA_CONSOLIDADA_NO_ENCONTRADA",
                "La partida no pertenece a la línea base aprobada del presupuesto indicado.");

        IReadOnlyList<Domain.Model.MovimientoPresupuestal> movimientos = await _movimientos.ListarConsolidadosAsync(
            query.IdPresupuesto, query.IdCatalogoPartida);

        return movimientos.Select(MovimientoConsolidadoResult.Desde).ToList();
    }
}
