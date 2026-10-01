using System.ComponentModel.DataAnnotations;

namespace Cobranzas_Vittoria.Contable.GastosDirectos.Presentation.Dto;

public sealed class GastoDirectoUpsertRequest
{
    [Range(1, int.MaxValue)]
    public int IdPresupuestoDetalle { get; set; }

    [Range(1, int.MaxValue)]
    public int? IdProveedor { get; set; }

    [Range(1, int.MaxValue)]
    public int IdMoneda { get; set; }

    public DateTime Fecha { get; set; }

    [Required, StringLength(250)]
    public string Concepto { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Descripcion { get; set; }

    [Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal Monto { get; set; }

    /// <summary>
    /// Sección de Gastos del proyecto (ADMINISTRATIVO, TERRENO, MARKETING_VENTAS, OTROS, MUNICIPAL). Si
    /// viene, SQL exige que la partida pertenezca a esa sección y que el centro de costo sea de un tipo admitido.
    /// </summary>
    [StringLength(30)]
    public string? Seccion { get; set; }

    /// <summary>Moneda original de la factura, solo como referencia (no se convierte).</summary>
    [Range(1, int.MaxValue)]
    public int? IdMonedaOriginal { get; set; }

    [Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal? MontoOriginal { get; set; }

    [Range(typeof(decimal), "0.000001", "999999999999.999999")]
    public decimal? TipoCambio { get; set; }

    public DateTime? FechaTipoCambio { get; set; }
}
