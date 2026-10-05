using Cobranzas_Vittoria.ControlPresupuestario.Application.Common;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.CentroCosto.Plantilla;

/// <summary>Plantilla vacía de importación de centros de costo: solo los encabezados esperados.</summary>
public sealed class ObtenerPlantillaCentroCostoHandler
{
    public IReadOnlyList<string> Handle(ObtenerPlantillaCentroCostoQuery query) => ColumnasImportacion.CentroCosto;
}
