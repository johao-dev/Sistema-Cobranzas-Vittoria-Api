namespace Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Importacion.Dtos;

/// <summary>
/// Fila del archivo de importación de centros de costo, tal como la escribe el
/// usuario: el tipo llega como código o nombre y el proyecto por su nombre.
/// </summary>
public sealed class CentroCostoImportDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string? Proyecto { get; set; }
    public string? Descripcion { get; set; }
    public int _Fila { get; set; }

    /// <summary>Errores de formato de la fila; se informan junto con los de negocio.</summary>
    internal List<Cobranzas_Vittoria.Application.Importacion.Excepciones.DetalleErrorFila> ErroresFormato { get; } = new();
}

/// <summary>
/// Fila que viaja al TVP ControlPresupuestario.TVP_CentroCosto.
/// El orden de las propiedades DEBE coincidir con el de las columnas del tipo.
/// </summary>
public sealed class CentroCostoImportTvpDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public int IdTipoCentroCosto { get; set; }
    public int? IdProyecto { get; set; }
    public string? Descripcion { get; set; }
    public int _Fila { get; set; }
}
