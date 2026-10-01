using System.ComponentModel.DataAnnotations;

namespace Cobranzas_Vittoria.ControlPresupuestario.Presentation.Dto.CentroCosto;

public sealed class CrearCentroCostoRequest
{
    [Required, StringLength(30, MinimumLength = 1)]
    public string Codigo { get; set; } = string.Empty;

    [Required, StringLength(150, MinimumLength = 1)]
    public string Nombre { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int IdTipoCentroCosto { get; set; }

    /// <summary>Solo para el tipo PROYECTO; null en centros corporativos.</summary>
    [Range(1, int.MaxValue)]
    public int? IdProyecto { get; set; }

    [StringLength(255)]
    public string? Descripcion { get; set; }
}
