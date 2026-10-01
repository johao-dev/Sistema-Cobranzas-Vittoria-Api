namespace Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Importacion.Dtos;

/// <summary>
/// Fila del archivo de importación del catálogo de partidas, tal como la
/// escribe el usuario: tipo y sección llegan como código o nombre y el padre
/// como código (puede ser otra fila del mismo archivo).
/// </summary>
public sealed class CatalogoPartidaImportDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string? CodigoPadre { get; set; }
    public string? Seccion { get; set; }
    public string? Descripcion { get; set; }
    public int _Fila { get; set; }

    /// <summary>
    /// Errores de formato de la fila. Se acumulan en vez de cortar el proceso para
    /// informarlos junto con los de negocio y que el usuario corrija todo de una vez.
    /// </summary>
    internal List<Cobranzas_Vittoria.Application.Importacion.Excepciones.DetalleErrorFila> ErroresFormato { get; } = new();
}

/// <summary>
/// Fila que viaja al TVP ControlPresupuestario.TVP_CatalogoPartida.
/// El orden de las propiedades DEBE coincidir con el de las columnas del tipo.
/// </summary>
public sealed class CatalogoPartidaImportTvpDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public int IdTipoPartida { get; set; }
    public string? CodigoPadre { get; set; }
    public int? IdSeccionGasto { get; set; }
    public string? Descripcion { get; set; }
    public int _Fila { get; set; }
}
