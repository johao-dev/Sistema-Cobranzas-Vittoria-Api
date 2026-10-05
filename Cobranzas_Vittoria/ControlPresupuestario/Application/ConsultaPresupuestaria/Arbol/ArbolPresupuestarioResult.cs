using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model.Consultas;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Service;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.ConsultaPresupuestaria.Arbol;

public sealed record EncabezadoArbol(int IdCentroCosto, string? CodigoCentroCosto, string? NombreCentroCosto,
    int? IdPresupuesto, int? IdPresupuestoVersion, string? EstadoPresupuesto, string? CodigoMoneda, string? SimboloMoneda);

/// <summary>Árbol de partidas con subtotales: nodos en preorden; los padres suman sus hojas descendientes.</summary>
public sealed record ArbolPresupuestarioResult(EncabezadoArbol Encabezado, TotalesArbol Totales, IReadOnlyList<NodoArbol> Nodos)
{
    /// <summary>Arma el árbol a partir de filas de saldo por detalle (vw_Saldo o vw_ControlPresupuestarioVigente).</summary>
    internal static async Task<ArbolPresupuestarioResult> ConstruirAsync(EncabezadoArbol encabezado,
        IEnumerable<SaldoPartidaFila> filas, ICatalogoPartidaRepository partidas)
    {
        var catalogo = (await partidas.ListarAsync(new FiltroPartidas()))
            .Select(p => new PartidaArbol(p.IdCatalogoPartida, p.Codigo, p.Nombre, p.IdPartidaPadre, p.Nivel))
            .ToList();
        var hojas = filas
            .Select(f => new MontosHojaArbol(f.IdCatalogoPartida, f.IdPresupuesto, f.IdPresupuestoVersion,
                f.IdPresupuestoDetalle, f.MontoPresupuestado, f.MontoComprometido, f.MontoEjecutado))
            .ToList();
        var (totales, nodos) = ArbolPresupuestario.Construir(hojas, catalogo);
        return new ArbolPresupuestarioResult(encabezado, totales, nodos);
    }
}
