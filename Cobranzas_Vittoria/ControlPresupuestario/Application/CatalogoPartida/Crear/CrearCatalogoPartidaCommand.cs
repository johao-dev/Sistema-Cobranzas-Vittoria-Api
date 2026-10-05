namespace Cobranzas_Vittoria.ControlPresupuestario.Application.CatalogoPartida.Crear;

public sealed record CrearCatalogoPartidaCommand(string Codigo, string Nombre, int IdTipoPartida, int? IdPartidaPadre,
    string? Descripcion, int? IdSeccionGasto);
