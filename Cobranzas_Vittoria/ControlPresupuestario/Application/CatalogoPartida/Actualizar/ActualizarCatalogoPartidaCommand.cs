namespace Cobranzas_Vittoria.ControlPresupuestario.Application.CatalogoPartida.Actualizar;

public sealed record ActualizarCatalogoPartidaCommand(int IdCatalogoPartida, string Nombre, int IdTipoPartida, bool Activo,
    int? IdPartidaPadre, string? Descripcion, int? IdSeccionGasto);
