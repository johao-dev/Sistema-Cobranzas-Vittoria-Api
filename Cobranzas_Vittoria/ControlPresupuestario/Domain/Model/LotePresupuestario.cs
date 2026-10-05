using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;

namespace Cobranzas_Vittoria.ControlPresupuestario.Domain.Model;

/// <summary>
/// Carga completa de montos sobre una versión BORRADOR, todo o nada. Con QuitarAusentes, las
/// partidas que no vienen en el lote se eliminan de la versión. Fila es la del archivo de origen
/// (0 cuando la carga viene del formulario).
/// </summary>
public sealed class LotePresupuestario
{
    public int IdPresupuestoVersion { get; }
    public bool QuitarAusentes { get; }
    public IReadOnlyList<Item> Items { get; }

    public sealed record Item(int IdCatalogoPartida, decimal MontoPresupuestado, string? Observacion, int Fila);

    private LotePresupuestario(int idPresupuestoVersion, bool quitarAusentes, IReadOnlyList<Item> items)
    {
        IdPresupuestoVersion = idPresupuestoVersion;
        QuitarAusentes = quitarAusentes;
        Items = items;
    }

    public static LotePresupuestario Crear(int idPresupuestoVersion, IEnumerable<Item> items, bool quitarAusentes)
    {
        var lista = items.ToList();
        if (lista.Count == 0 && !quitarAusentes)
            throw new ValidacionPresupuestariaException("LOTE_VACIO", "La carga no trae ninguna partida.");
        if (lista.GroupBy(i => i.IdCatalogoPartida).Any(g => g.Count() > 1))
            throw new ValidacionPresupuestariaException("PARTIDA_DUPLICADA", "Una partida no puede venir más de una vez en la carga.");
        var normalizados = lista
            .Select(i => i with
            {
                IdCatalogoPartida = Reglas.Id(i.IdCatalogoPartida, "IdCatalogoPartida"),
                MontoPresupuestado = Reglas.Monto(i.MontoPresupuestado, "El monto presupuestado"),
                Observacion = Reglas.Opcional(i.Observacion, "Observacion", 500)
            })
            .ToList();
        return new LotePresupuestario(Reglas.Id(idPresupuestoVersion, "IdPresupuestoVersion"), quitarAusentes, normalizados);
    }
}

public sealed record ResultadoCargaLote(int IdPresupuestoVersion, int Agregados, int Actualizados, int Eliminados,
    int PartidasEnVersion, decimal MontoTotal);
