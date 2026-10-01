using Cobranzas_Vittoria.ControlPresupuestario.Application.ConsultaPresupuestaria;

namespace Cobranzas_Vittoria.ControlPresupuestario.Presentation.Dto.ConsultaPresupuestaria;

/// <summary>Filtros de lectura de las consultas presupuestarias (query string).</summary>
public sealed class ConsultaPresupuestariaRequest
{
    public int? IdCentroCosto { get; set; }
    public int? IdPresupuesto { get; set; }
    public int? IdPresupuestoVersion { get; set; }
    public int? IdCatalogoPartida { get; set; }
    public int? IdMoneda { get; set; }
    public string? EstadoPresupuesto { get; set; }
    public bool? SoloExcedidos { get; set; }
    /// <summary>true para incluir presupuestos inactivos (por defecto se descartan).</summary>
    public bool? IncluirInactivos { get; set; }

    public ConsultaPresupuestariaQuery AQuery() => new(IdCentroCosto, IdPresupuesto, IdPresupuestoVersion,
        IdCatalogoPartida, IdMoneda, EstadoPresupuesto, SoloExcedidos, IncluirInactivos);
}
