using Cobranzas_Vittoria.ControlPresupuestario.Application.Common;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.CatalogoPartida.Plantilla;

/// <summary>Plantilla vacía de importación de partidas del catálogo: solo los encabezados esperados.</summary>
public sealed class ObtenerPlantillaCatalogoPartidaHandler
{
    public IReadOnlyList<string> Handle(ObtenerPlantillaCatalogoPartidaQuery query) => ColumnasImportacion.CatalogoPartida;
}
