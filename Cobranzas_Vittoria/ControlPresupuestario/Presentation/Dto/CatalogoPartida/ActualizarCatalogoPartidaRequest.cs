using System.ComponentModel.DataAnnotations;

namespace Cobranzas_Vittoria.ControlPresupuestario.Presentation.Dto.CatalogoPartida;

public sealed class ActualizarCatalogoPartidaRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string Nombre { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int IdTipoPartida { get; set; }

    public bool Activo { get; set; } = true;

    [Range(1, int.MaxValue)]
    public int? IdPartidaPadre { get; set; }

    [StringLength(500)]
    public string? Descripcion { get; set; }

    [Range(1, int.MaxValue)]
    public int? IdSeccionGasto { get; set; }
}
