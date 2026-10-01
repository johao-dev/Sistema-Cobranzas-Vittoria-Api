using System.ComponentModel.DataAnnotations;

namespace Cobranzas_Vittoria.ControlPresupuestario.Presentation.Dto.PresupuestoVersion;

public sealed class AnularVersionRequest
{
    [Required, StringLength(500, MinimumLength = 1)]
    public string Motivo { get; set; } = string.Empty;
}
