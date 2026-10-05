using System.ComponentModel.DataAnnotations;

namespace Cobranzas_Vittoria.ControlPresupuestario.Presentation.Dto.PresupuestoDetalle;

public sealed class ActualizarPartidaRequest
{
    [Range(typeof(decimal), "0", "9999999999999999.99")]
    public decimal MontoPresupuestado { get; set; }

    [StringLength(500)]
    public string? Observacion { get; set; }
}
