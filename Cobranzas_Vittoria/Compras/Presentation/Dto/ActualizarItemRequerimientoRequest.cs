using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Cobranzas_Vittoria.Compras.Domain.ValueObject;

namespace Cobranzas_Vittoria.Compras.Presentation.Dto;

/// <summary>Omitir conserva; null limpia partida/observación. La cantidad informada nunca puede ser null.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class ActualizarItemRequerimientoRequest
{
    private decimal? _cantidad;
    private int? _idPresupuestoDetalle;
    private string? _observacion;

    [JsonIgnore]
    public CamposItemRequerimiento Campos { get; private set; }

    [Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal? Cantidad
    {
        get => _cantidad;
        set { _cantidad = value; Campos |= CamposItemRequerimiento.Cantidad; }
    }

    [Range(1, int.MaxValue)]
    public int? IdPresupuestoDetalle
    {
        get => _idPresupuestoDetalle;
        set { _idPresupuestoDetalle = value; Campos |= CamposItemRequerimiento.Partida; }
    }

    [StringLength(250)]
    public string? Observacion
    {
        get => _observacion;
        set { _observacion = value; Campos |= CamposItemRequerimiento.Observacion; }
    }
}
