using System.ComponentModel.DataAnnotations;

namespace Cobranzas_Vittoria.ControlPresupuestario.Presentation.Dto.PresupuestoDetalle;

/// <summary>Ajuste manual sobre el ledger: corrige compromiso o ejecución sin borrar historia.</summary>
public sealed class RegistrarAjusteRequest
{
    /// <summary>COMPROMISO o EJECUCION.</summary>
    [Required]
    public string Afectacion { get; set; } = string.Empty;

    /// <summary>INCREMENTO o DECREMENTO.</summary>
    [Required]
    public string Direccion { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal Monto { get; set; }

    [Required, StringLength(500, MinimumLength = 1)]
    public string Observacion { get; set; } = string.Empty;

    public DateTime? Fecha { get; set; }
}
