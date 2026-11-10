namespace Cobranzas_Vittoria.Compras.Application.Requerimiento.Crear;

public sealed record ContextoCreacionRequerimiento(
    bool ExisteProyecto,
    bool ExisteSolicitante,
    IReadOnlySet<int> MaterialesExistentes,
    IReadOnlyList<PartidaCreacionRequerimiento> Partidas);

public sealed class PartidaCreacionRequerimiento
{
    public int IdPresupuestoDetalle { get; init; }
    public string EstadoPresupuesto { get; init; } = string.Empty;
    public bool PresupuestoActivo { get; init; }
    public bool CentroCostoActivo { get; init; }
    public int? IdProyecto { get; init; }
}

public sealed record DisponibilidadNumeroRequerimiento(bool NumeroSolicitadoExiste, long MaximoNumero);
