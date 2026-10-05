using Cobranzas_Vittoria.Application.Common.Exports;

namespace Cobranzas_Vittoria.ControlPresupuestario.Presentation.Http;

public sealed class CentroCostoPlantillaFila
{
    [ExcelColumn(Header = "Codigo", Order = 1, Width = 16)] public string Codigo { get; set; } = string.Empty;
    [ExcelColumn(Header = "Nombre", Order = 2, Width = 40)] public string Nombre { get; set; } = string.Empty;
    [ExcelColumn(Header = "Tipo", Order = 3, Width = 18)] public string Tipo { get; set; } = string.Empty;
    [ExcelColumn(Header = "Proyecto", Order = 4, Width = 36)] public string Proyecto { get; set; } = string.Empty;
    [ExcelColumn(Header = "Descripcion", Order = 5, Width = 40)] public string Descripcion { get; set; } = string.Empty;
}

public sealed class CatalogoPartidaPlantillaFila
{
    [ExcelColumn(Header = "Codigo", Order = 1, Width = 14)] public string Codigo { get; set; } = string.Empty;
    [ExcelColumn(Header = "Nombre", Order = 2, Width = 40)] public string Nombre { get; set; } = string.Empty;
    [ExcelColumn(Header = "Tipo", Order = 3, Width = 18)] public string Tipo { get; set; } = string.Empty;
    [ExcelColumn(Header = "CodigoPadre", Order = 4, Width = 14)] public string CodigoPadre { get; set; } = string.Empty;
    [ExcelColumn(Header = "Seccion", Order = 5, Width = 20)] public string Seccion { get; set; } = string.Empty;
    [ExcelColumn(Header = "Descripcion", Order = 6, Width = 40)] public string Descripcion { get; set; } = string.Empty;
}

public sealed class PresupuestoPlantillaFila
{
    [ExcelColumn(Header = "CodigoPartida", Order = 1, Width = 16)] public string CodigoPartida { get; set; } = string.Empty;
    [ExcelColumn(Header = "Partida", Order = 2, Width = 45)] public string Partida { get; set; } = string.Empty;
    [ExcelColumn(Header = "Monto", Order = 3, Width = 16)] public decimal Monto { get; set; }
    [ExcelColumn(Header = "Observacion", Order = 4, Width = 40)] public string Observacion { get; set; } = string.Empty;
}

/// <summary>Codigo va como texto: así Excel no convierte 1.10 en 1.1.</summary>
public sealed class EstructuraPresupuestoPlantillaFila
{
    [ExcelColumn(Header = "Codigo", Order = 1, Width = 14)] public string Codigo { get; set; } = string.Empty;
    [ExcelColumn(Header = "Nombre", Order = 2, Width = 50)] public string Nombre { get; set; } = string.Empty;
    [ExcelColumn(Header = "Tipo", Order = 3, Width = 16)] public string Tipo { get; set; } = string.Empty;
    [ExcelColumn(Header = "Seccion", Order = 4, Width = 18)] public string Seccion { get; set; } = string.Empty;
    [ExcelColumn(Header = "Monto", Order = 5, Width = 16)] public decimal? Monto { get; set; }
    [ExcelColumn(Header = "Subtotal", Order = 6, Width = 16)] public decimal? Subtotal { get; set; }
    [ExcelColumn(Header = "Observacion", Order = 7, Width = 40)] public string Observacion { get; set; } = string.Empty;
}
