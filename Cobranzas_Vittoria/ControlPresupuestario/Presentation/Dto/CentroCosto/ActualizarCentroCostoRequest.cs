using System.ComponentModel.DataAnnotations;

namespace Cobranzas_Vittoria.ControlPresupuestario.Presentation.Dto.CentroCosto;

/// <summary>Codigo e IdTipoCentroCosto son opcionales y no editables: si vienen, deben coincidir con los actuales.</summary>
public sealed class ActualizarCentroCostoRequest
{
    [StringLength(30)]
    public string? Codigo { get; set; }

    [Required, StringLength(150, MinimumLength = 1)]
    public string Nombre { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int? IdTipoCentroCosto { get; set; }

    [Range(1, int.MaxValue)]
    public int? IdProyecto { get; set; }

    public bool Activo { get; set; } = true;

    [StringLength(255)]
    public string? Descripcion { get; set; }
}
