using System.ComponentModel.DataAnnotations;

namespace Cobranzas_Vittoria.ControlPresupuestario.Presentation.Dto.CatalogoPartida;

public sealed class CrearCatalogoPartidaRequest
{
    [Required, StringLength(50, MinimumLength = 1)]
    public string Codigo { get; set; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 1)]
    public string Nombre { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int IdTipoPartida { get; set; }

    [Range(1, int.MaxValue)]
    public int? IdPartidaPadre { get; set; }

    [StringLength(500)]
    public string? Descripcion { get; set; }

    /// <summary>Sección de gasto directo; solo aplica a partidas sin hijas.</summary>
    [Range(1, int.MaxValue)]
    public int? IdSeccionGasto { get; set; }
}
