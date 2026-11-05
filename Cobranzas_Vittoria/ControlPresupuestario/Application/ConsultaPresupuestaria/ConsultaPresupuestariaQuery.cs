using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model.Consultas;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.ConsultaPresupuestaria;

/// <summary>
/// Filtros de lectura que acepta cualquier consulta presupuestaria. Por defecto los reportes muestran
/// lo vigente: sin estado ni versión indicados solo entran las versiones APROBADAS, y sin
/// IncluirInactivos=true se descartan los presupuestos inactivos. Un borrador o una versión
/// histórica se consultan pidiendo su estado o su versión. Consolidado habilita el acumulado
/// APROBADO/HISTORICO sobre la línea base vigente en los seis reportes del Panel.
/// </summary>
public sealed record ConsultaPresupuestariaQuery(int? IdCentroCosto, int? IdPresupuesto, int? IdPresupuestoVersion,
    int? IdCatalogoPartida, int? IdMoneda, string? EstadoPresupuesto, bool? IncluirInactivos = null,
    bool Consolidado = false)
{
    public const string EstadoPorDefecto = "APROBADO";

    public FiltroConsultaPresupuestaria AFiltro()
    {
        if (Consolidado && (IdPresupuestoVersion.HasValue || !string.IsNullOrWhiteSpace(EstadoPresupuesto)))
            throw new ValidacionPresupuestariaException("FILTROS_INCOMPATIBLES",
                "Consolidado no admite idPresupuestoVersion ni estadoPresupuesto explícitos.");

        return new FiltroConsultaPresupuestaria(IdCentroCosto, IdPresupuesto,
        IdPresupuestoVersion, IdCatalogoPartida, IdMoneda,
        string.IsNullOrWhiteSpace(EstadoPresupuesto) && IdPresupuestoVersion is null ? EstadoPorDefecto : EstadoPresupuesto,
        SoloPresupuestosActivos: IncluirInactivos != true).Normalizado();
    }
}
