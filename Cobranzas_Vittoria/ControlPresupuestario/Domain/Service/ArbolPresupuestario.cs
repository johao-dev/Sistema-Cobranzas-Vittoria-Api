namespace Cobranzas_Vittoria.ControlPresupuestario.Domain.Service;

/// <summary>Partida del catálogo tal como la necesita el árbol: identidad y posición en la jerarquía.</summary>
public sealed record PartidaArbol(int IdCatalogoPartida, string Codigo, string Nombre, int? IdPartidaPadre, int Nivel);

/// <summary>Montos de una partida hoja en un detalle de versión (una fila por detalle).</summary>
public sealed record MontosHojaArbol(int IdCatalogoPartida, int? IdPresupuesto, int? IdPresupuestoVersion,
    int? IdPresupuestoDetalle, decimal MontoPresupuestado, decimal MontoComprometido, decimal MontoEjecutado);

/// <summary>
/// Nodo del árbol. En una hoja, IdPresupuesto/IdPresupuestoVersion/IdPresupuestoDetalle identifican
/// su detalle de versión; son null en las categorías y en una hoja que suma detalles de varios presupuestos.
/// </summary>
public sealed record NodoArbol(int IdCatalogoPartida, string Codigo, string Nombre, int? IdPartidaPadre, int Nivel,
    bool EsHoja, int CantidadHijas, int CantidadHojas, int? IdPresupuesto, int? IdPresupuestoVersion,
    int? IdPresupuestoDetalle, decimal MontoPresupuestado, decimal MontoComprometido, decimal MontoEjecutado,
    decimal SaldoDisponible, decimal PorcentajeComprometido, decimal PorcentajeEjecutado, bool Excedido,
    int PartidasExcedidas);

public sealed record TotalesArbol(decimal MontoPresupuestado, decimal MontoComprometido, decimal MontoEjecutado,
    decimal SaldoDisponible, decimal PorcentajeComprometido, decimal PorcentajeEjecutado, int CantidadPartidas,
    int PartidasExcedidas);

/// <summary>
/// Arma el árbol de partidas de un presupuesto con subtotales: parte de las hojas con monto, sube por
/// el catálogo hasta las raíces y suma en cada categoría los montos de sus hojas descendientes, una
/// sola vez cada una. Devuelve los nodos en preorden, con orden natural de código (1.2 antes que 1.10).
/// </summary>
public static class ArbolPresupuestario
{
    public static (TotalesArbol Totales, IReadOnlyList<NodoArbol> Nodos) Construir(
        IReadOnlyCollection<MontosHojaArbol> hojas, IReadOnlyCollection<PartidaArbol> catalogo)
    {
        var partidas = catalogo.ToDictionary(p => p.IdCatalogoPartida);
        var montos = hojas
            .GroupBy(h => h.IdCatalogoPartida)
            .ToDictionary(g => g.Key, g =>
            {
                var unico = g.Count() == 1 ? g.First() : null;
                return new MontosHojaArbol(g.Key, unico?.IdPresupuesto, unico?.IdPresupuestoVersion,
                    unico?.IdPresupuestoDetalle, g.Sum(h => h.MontoPresupuestado), g.Sum(h => h.MontoComprometido),
                    g.Sum(h => h.MontoEjecutado));
            });

        // Partidas del árbol: cada hoja con monto y todos sus ancestros. Un ancestro que no está en el
        // catálogo recibido corta la subida y su descendiente queda como raíz.
        var incluidas = new HashSet<int>();
        foreach (var id in montos.Keys)
        {
            int? actual = id;
            while (actual is int i && partidas.ContainsKey(i) && incluidas.Add(i))
                actual = partidas[i].IdPartidaPadre;
        }

        int? PadreEnArbol(PartidaArbol p) => p.IdPartidaPadre is int padre && incluidas.Contains(padre) ? padre : null;
        var hijas = incluidas
            .Select(id => partidas[id])
            .ToLookup(PadreEnArbol);

        var nodos = new List<NodoArbol>(incluidas.Count);
        foreach (var raiz in Ordenar(hijas[null]))
            Visitar(raiz, null);

        var hojasArbol = nodos.Where(n => n.EsHoja).ToList();
        var totales = new TotalesArbol(
            hojasArbol.Sum(n => n.MontoPresupuestado), hojasArbol.Sum(n => n.MontoComprometido),
            hojasArbol.Sum(n => n.MontoEjecutado), hojasArbol.Sum(n => n.SaldoDisponible),
            Porcentaje(hojasArbol.Sum(n => n.MontoComprometido), hojasArbol.Sum(n => n.MontoPresupuestado)),
            Porcentaje(hojasArbol.Sum(n => n.MontoEjecutado), hojasArbol.Sum(n => n.MontoPresupuestado)),
            nodos.Count, hojasArbol.Count(n => n.Excedido));
        return (totales, nodos);

        // Inserta el nodo en su posición de preorden y devuelve sus montos acumulados.
        Acumulado Visitar(PartidaArbol partida, int? idPadre)
        {
            var posicion = nodos.Count;
            nodos.Add(null!);
            var propias = Ordenar(hijas[partida.IdCatalogoPartida]).ToList();
            Acumulado suma;
            MontosHojaArbol? m = null;
            if (propias.Count == 0)
            {
                m = montos.GetValueOrDefault(partida.IdCatalogoPartida)
                    ?? new MontosHojaArbol(partida.IdCatalogoPartida, null, null, null, 0, 0, 0);
                var saldo = m.MontoPresupuestado - m.MontoComprometido - m.MontoEjecutado;
                suma = new Acumulado(m.MontoPresupuestado, m.MontoComprometido, m.MontoEjecutado, 1, saldo < 0 ? 1 : 0);
            }
            else
            {
                suma = propias.Select(h => Visitar(h, partida.IdCatalogoPartida)).Aggregate(Acumulado.Cero, Acumulado.Sumar);
            }

            var saldoNodo = suma.Presupuestado - suma.Comprometido - suma.Ejecutado;
            nodos[posicion] = new NodoArbol(partida.IdCatalogoPartida, partida.Codigo, partida.Nombre, idPadre,
                partida.Nivel, propias.Count == 0, propias.Count, suma.Hojas, m?.IdPresupuesto, m?.IdPresupuestoVersion,
                m?.IdPresupuestoDetalle, suma.Presupuestado, suma.Comprometido,
                suma.Ejecutado, saldoNodo, Porcentaje(suma.Comprometido, suma.Presupuestado),
                Porcentaje(suma.Ejecutado, suma.Presupuestado), saldoNodo < 0, suma.Excedidas);
            return suma;
        }
    }

    /// <summary>Compara códigos por segmentos: los numéricos por valor, el resto como texto.</summary>
    public static int CompararCodigos(string? a, string? b)
    {
        var sa = (a ?? string.Empty).Split('.');
        var sb = (b ?? string.Empty).Split('.');
        for (var i = 0; i < Math.Min(sa.Length, sb.Length); i++)
        {
            var c = long.TryParse(sa[i], out var na) && long.TryParse(sb[i], out var nb)
                ? na.CompareTo(nb)
                : string.Compare(sa[i], sb[i], StringComparison.OrdinalIgnoreCase);
            if (c != 0) return c;
        }
        return sa.Length.CompareTo(sb.Length);
    }

    private static IEnumerable<PartidaArbol> Ordenar(IEnumerable<PartidaArbol> partidas)
        => partidas.OrderBy(p => p.Codigo, Comparer<string>.Create(CompararCodigos));

    private static decimal Porcentaje(decimal parte, decimal total)
        => total == 0 ? 0 : decimal.Round(parte * 100 / total, 2);

    private sealed record Acumulado(decimal Presupuestado, decimal Comprometido, decimal Ejecutado, int Hojas, int Excedidas)
    {
        public static readonly Acumulado Cero = new(0, 0, 0, 0, 0);

        public static Acumulado Sumar(Acumulado a, Acumulado b) => new(a.Presupuestado + b.Presupuestado,
            a.Comprometido + b.Comprometido, a.Ejecutado + b.Ejecutado, a.Hojas + b.Hojas, a.Excedidas + b.Excedidas);
    }
}
