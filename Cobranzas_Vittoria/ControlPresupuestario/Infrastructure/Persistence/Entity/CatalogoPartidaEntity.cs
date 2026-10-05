namespace Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Entity;

/// <summary>Fila de usp_CatalogoPartida_Listar / usp_CatalogoPartida_Obtener.</summary>
public sealed class CatalogoPartidaEntity
{
    public int IdCatalogoPartida { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int? IdPartidaPadre { get; set; }
    public string? CodigoPartidaPadre { get; set; }
    public string? NombrePartidaPadre { get; set; }
    public int Nivel { get; set; }
    public int IdTipoPartida { get; set; }
    public string? CodigoTipoPartida { get; set; }
    public string? NombreTipoPartida { get; set; }
    public bool Activo { get; set; }
    public bool EsHoja { get; set; }
    public DateTime? FechaCreacion { get; set; }
    public DateTime? FechaActualizacion { get; set; }
    public int? IdSeccionGasto { get; set; }
    public string? CodigoSeccionGasto { get; set; }
    public string? NombreSeccionGasto { get; set; }
}
