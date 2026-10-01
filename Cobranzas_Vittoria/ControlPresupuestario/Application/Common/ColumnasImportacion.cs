namespace Cobranzas_Vittoria.ControlPresupuestario.Application.Common;

/// <summary>Encabezados de las plantillas de importación de los maestros del módulo.</summary>
public static class ColumnasImportacion
{
    public static readonly string[] CentroCosto = { "Codigo", "Nombre", "Tipo", "Proyecto", "Descripcion" };
    public static readonly string[] CatalogoPartida = { "Codigo", "Nombre", "Tipo", "CodigoPadre", "Seccion", "Descripcion" };
    public static readonly string[] PresupuestoDetalle = { "CodigoPartida", "Partida", "Monto", "Observacion" };
    public static readonly string[] EstructuraPresupuesto = { "Codigo", "Nombre", "Tipo", "Seccion", "Monto", "Subtotal", "Observacion" };
}
