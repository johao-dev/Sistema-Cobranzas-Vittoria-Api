using System.ComponentModel.DataAnnotations;

namespace Cobranzas_Vittoria.ControlPresupuestario.Presentation.Dto.Presupuesto;

/// <summary>La moneda y el centro de costo no se editan: definen la historia económica del presupuesto.</summary>
public sealed class ActualizarPresupuestoRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string Nombre { get; set; } = string.Empty;

    public bool Activo { get; set; } = true;

    [StringLength(500)]
    public string? Descripcion { get; set; }

    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }

    /// <summary>true para inactivar aunque tenga gastos, compromisos o requerimientos asociados.</summary>
    public bool ConfirmarInactivacion { get; set; }
}
