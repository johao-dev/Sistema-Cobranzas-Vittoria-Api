using Cobranzas_Vittoria.ControlPresupuestario.Domain.ValueObject;

namespace Cobranzas_Vittoria.ControlPresupuestario.Domain.Model.Consultas;

/// <summary>
/// Filtros de lectura comunes a las consultas presupuestarias. SoloPresupuestosActivos descarta las
/// filas de presupuestos inactivos.
/// </summary>
public sealed record FiltroConsultaPresupuestaria(
    int? IdCentroCosto = null,
    int? IdPresupuesto = null,
    int? IdPresupuestoVersion = null,
    int? IdCatalogoPartida = null,
    int? IdMoneda = null,
    string? EstadoPresupuesto = null,
    bool? SoloExcedidos = null,
    bool SoloPresupuestosActivos = false)
{
    /// <summary>Devuelve el filtro con el estado normalizado; lanza 400 si el estado no existe.</summary>
    public FiltroConsultaPresupuestaria Normalizado()
        => string.IsNullOrWhiteSpace(EstadoPresupuesto)
            ? this with { EstadoPresupuesto = null }
            : this with { EstadoPresupuesto = ValueObject.EstadoPresupuesto.NormalizarCodigo(EstadoPresupuesto) };
}
