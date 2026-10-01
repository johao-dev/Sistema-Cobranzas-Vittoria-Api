using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model.Consultas;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.ConsultaPresupuestaria;

/// <summary>
/// Filtros de lectura que acepta cualquier consulta presupuestaria. Por defecto los reportes muestran
/// lo vigente: sin estado ni versión indicados solo entran las versiones APROBADAS, y sin
/// IncluirInactivos=true se descartan los presupuestos inactivos. Un borrador o una versión
/// histórica se consultan pidiendo su estado o su versión.
/// </summary>
public sealed record ConsultaPresupuestariaQuery(int? IdCentroCosto, int? IdPresupuesto, int? IdPresupuestoVersion,
    int? IdCatalogoPartida, int? IdMoneda, string? EstadoPresupuesto, bool? SoloExcedidos, bool? IncluirInactivos = null)
{
    public const string EstadoPorDefecto = "APROBADO";

    public FiltroConsultaPresupuestaria AFiltro() => new FiltroConsultaPresupuestaria(IdCentroCosto, IdPresupuesto,
        IdPresupuestoVersion, IdCatalogoPartida, IdMoneda,
        string.IsNullOrWhiteSpace(EstadoPresupuesto) && IdPresupuestoVersion is null ? EstadoPorDefecto : EstadoPresupuesto,
        SoloExcedidos, SoloPresupuestosActivos: IncluirInactivos != true).Normalizado();
}
