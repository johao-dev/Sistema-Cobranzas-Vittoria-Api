using Cobranzas_Vittoria.ControlPresupuestario.Application.Common;
using Cobranzas_Vittoria.ControlPresupuestario.Application.ConsultaPresupuestaria.Arbol;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model.Consultas;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.PresupuestoVersion.Arbol;

public sealed record ObtenerArbolVersionQuery(int IdPresupuesto, int IdPresupuestoVersion);

/// <summary>
/// Árbol de partidas de una versión en cualquier estado (vw_Saldo): sirve para revisar padres, hijas y
/// subtotales de un BORRADOR recién cargado antes de aprobarlo.
/// </summary>
public sealed class ObtenerArbolVersionHandler
{
    private readonly IPresupuestoRepository _presupuestos;
    private readonly IPresupuestoVersionRepository _versiones;
    private readonly IConsultaPresupuestariaRepository _consultas;
    private readonly ICatalogoPartidaRepository _partidas;

    public ObtenerArbolVersionHandler(IPresupuestoRepository presupuestos, IPresupuestoVersionRepository versiones,
        IConsultaPresupuestariaRepository consultas, ICatalogoPartidaRepository partidas)
    {
        _presupuestos = presupuestos;
        _versiones = versiones;
        _consultas = consultas;
        _partidas = partidas;
    }

    public async Task<ArbolPresupuestarioResult> HandleAsync(ObtenerArbolVersionQuery query)
    {
        PresupuestoVersionValidator.ValidarIds(query.IdPresupuesto, query.IdPresupuestoVersion);
        var version = await _versiones.ObtenerDelPresupuestoAsync(query.IdPresupuesto, query.IdPresupuestoVersion);
        var presupuesto = await _presupuestos.ObtenerAsync(query.IdPresupuesto)
            ?? throw new PresupuestoNoEncontradoException(query.IdPresupuesto);
        var filas = await _consultas.SaldoAsync(new FiltroConsultaPresupuestaria(IdPresupuestoVersion: query.IdPresupuestoVersion));

        var encabezado = new EncabezadoArbol(presupuesto.IdCentroCosto, presupuesto.CodigoCentroCosto,
            presupuesto.NombreCentroCosto, presupuesto.IdPresupuesto, version.IdPresupuestoVersion, version.Estado,
            presupuesto.CodigoMoneda, presupuesto.SimboloMoneda);
        return await ArbolPresupuestarioResult.ConstruirAsync(encabezado, filas, _partidas);
    }
}
