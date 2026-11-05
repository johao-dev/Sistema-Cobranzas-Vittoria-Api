using Cobranzas_Vittoria.ControlPresupuestario.Application.ConsultaPresupuestaria;

namespace Cobranzas_Vittoria.ControlPresupuestario.Presentation.Dto.ConsultaPresupuestaria;

/// <summary>Filtros de lectura de las consultas presupuestarias (query string).</summary>
public class ConsultaPresupuestariaRequest
{
    public int? IdCentroCosto { get; set; }
    public int? IdPresupuesto { get; set; }
    public int? IdPresupuestoVersion { get; set; }
    public int? IdCatalogoPartida { get; set; }
    public int? IdMoneda { get; set; }
    public string? EstadoPresupuesto { get; set; }
    /// <summary>true para incluir presupuestos inactivos (por defecto se descartan).</summary>
    public bool? IncluirInactivos { get; set; }

    public virtual ConsultaPresupuestariaQuery AQuery() => new(IdCentroCosto, IdPresupuesto, IdPresupuestoVersion,
        IdCatalogoPartida, IdMoneda, EstadoPresupuesto, IncluirInactivos);
}

/// <summary>Modo aditivo disponible en los seis reportes del Panel de Control.</summary>
public sealed class ReportePresupuestarioRequest : ConsultaPresupuestariaRequest
{
    public bool Consolidado { get; set; }

    public override ConsultaPresupuestariaQuery AQuery() => base.AQuery() with { Consolidado = Consolidado };
}
