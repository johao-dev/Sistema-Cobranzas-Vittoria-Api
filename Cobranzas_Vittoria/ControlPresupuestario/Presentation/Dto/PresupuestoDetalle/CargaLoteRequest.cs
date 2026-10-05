using System.ComponentModel.DataAnnotations;

namespace Cobranzas_Vittoria.ControlPresupuestario.Presentation.Dto.PresupuestoDetalle;

/// <summary>Carga completa de montos (todo o nada). Con QuitarAusentes, las partidas que no vienen se eliminan.</summary>
public sealed class CargaLoteRequest
{
    [Required]
    public List<CargaLoteItemRequest> Detalles { get; set; } = new();

    public bool QuitarAusentes { get; set; }
}

public sealed class CargaLoteItemRequest
{
    [Range(1, int.MaxValue)]
    public int IdCatalogoPartida { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999.99")]
    public decimal MontoPresupuestado { get; set; }

    /// <summary>NULL o vacío conserva la observación que ya tenía la partida.</summary>
    [StringLength(500)]
    public string? Observacion { get; set; }
}
