namespace Cobranzas_Vittoria.ControlPresupuestario.Domain.Model;

/// <summary>Partida que la importación crea en el catálogo; el padre puede ser otra partida nueva del mismo lote.</summary>
public sealed record PartidaNuevaEstructura(string Codigo, string Nombre, int IdTipoPartida, string? CodigoPadre,
    int? IdSeccionGasto, int Fila);

/// <summary>Monto de una partida hoja, identificada por código porque puede nacer en la misma importación.</summary>
public sealed record MontoEstructura(string CodigoPartida, decimal Monto, string? Observacion, int Fila);

/// <summary>
/// Carga de un presupuesto jerárquico sobre una versión BORRADOR: crea en el catálogo las partidas que
/// faltan y carga los montos de las hojas, en una sola transacción.
/// </summary>
public sealed record ImportacionEstructura(int IdPresupuestoVersion, IReadOnlyList<PartidaNuevaEstructura> PartidasNuevas,
    IReadOnlyList<MontoEstructura> Montos, bool QuitarAusentes, string Usuario);

public sealed record ResultadoImportacionEstructura(ResultadoCargaLote Lote, int PartidasCreadas);
