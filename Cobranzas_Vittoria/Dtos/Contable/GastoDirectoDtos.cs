using System.ComponentModel.DataAnnotations;

namespace Cobranzas_Vittoria.Dtos.Contable;

public sealed class GastoDirectoUpsertDto
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
}

public sealed record GastoDirectoDocumentoNuevo(
    string TipoDocumento,
    string NombreArchivo,
    string RutaArchivo,
    string? Extension);
