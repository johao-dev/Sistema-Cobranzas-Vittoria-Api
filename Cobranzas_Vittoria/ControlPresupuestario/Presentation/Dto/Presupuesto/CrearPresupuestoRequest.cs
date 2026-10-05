using System.ComponentModel.DataAnnotations;

namespace Cobranzas_Vittoria.ControlPresupuestario.Presentation.Dto.Presupuesto;

public sealed class CrearPresupuestoRequest
{
    [Range(1, int.MaxValue)]
    public int IdCentroCosto { get; set; }

    [Range(1, int.MaxValue)]
    public int IdMoneda { get; set; }

    [Required, StringLength(50, MinimumLength = 1)]
    public string Codigo { get; set; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 1)]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Descripcion { get; set; }

    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
}
