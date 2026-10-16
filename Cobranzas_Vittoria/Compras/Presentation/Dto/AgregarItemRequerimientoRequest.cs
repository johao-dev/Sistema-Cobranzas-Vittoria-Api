using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Cobranzas_Vittoria.Compras.Presentation.Dto;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class AgregarItemRequerimientoRequest
{
    [Range(1, int.MaxValue)]
    public int IdMaterial { get; init; }

    [Required, Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal? Cantidad { get; init; }

    [Range(1, int.MaxValue)]
    public int? IdPresupuestoDetalle { get; init; }

    [StringLength(250)]
    public string? Observacion { get; init; }
}
