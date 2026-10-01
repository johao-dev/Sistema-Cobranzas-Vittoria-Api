namespace Cobranzas_Vittoria.ControlPresupuestario.Application.PresupuestoDetalle.CargarLote;

public sealed record CargarLoteCommand(int IdPresupuesto, int IdPresupuestoVersion, IReadOnlyList<CargarLoteItem> Detalles,
    bool QuitarAusentes);

/// <summary>Observacion vacía conserva la que ya tenía la partida.</summary>
public sealed record CargarLoteItem(int IdCatalogoPartida, decimal MontoPresupuestado, string? Observacion);
