namespace Cobranzas_Vittoria.ControlPresupuestario.Application.ConsultaPresupuestaria.Dashboard;

/// <summary>
/// Nivel: agrupa rubros y ejecución en la partida ancestra de ese nivel (1 = raíces); null = partidas finales.
/// IdPartidaPadre: limita el tablero a esa rama (totales, curva y desviaciones) y, sin nivel, agrupa por sus hijas.
/// </summary>
public sealed record ObtenerDashboardQuery(int IdCentroCosto, int? IdPresupuesto, int? Nivel = null, int? IdPartidaPadre = null);
