using System.ComponentModel.DataAnnotations;
using Cobranzas_Vittoria.ControlPresupuestario.Application.MovimientoPresupuestal.ListarConsolidados;

namespace Cobranzas_Vittoria.ControlPresupuestario.Presentation.Dto.ConsultaPresupuestaria;

public sealed class MovimientosConsolidadosRequest
{
    [Range(1, int.MaxValue)]
    public int IdPresupuesto { get; set; }

    [Range(1, int.MaxValue)]
    public int IdCatalogoPartida { get; set; }

    public ListarMovimientosConsolidadosQuery AQuery() => new(IdPresupuesto, IdCatalogoPartida);
}
