using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model;

namespace Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;


/// <summary>Puerto de persistencia de las partidas de una versión.</summary>
public interface IPresupuestoDetalleRepository
{
    Task<IReadOnlyList<PresupuestoDetalle>> ListarPorVersionAsync(int idPresupuestoVersion);
    Task<int> AgregarAsync(PresupuestoDetalle detalle);
    Task ActualizarAsync(PresupuestoDetalle detalle);
    Task EliminarAsync(int idPresupuestoDetalle);
    Task<ResultadoCargaLote> CargarLoteAsync(LotePresupuestario lote);

    /// <summary>
    /// Crea las partidas nuevas del catálogo y carga los montos de la versión en una sola transacción:
    /// si falla cualquiera de los dos pasos no queda nada. Un rechazo por fila del catálogo sale como
    /// DatosInvalidosException (422).
    /// </summary>
    Task<ResultadoImportacionEstructura> ImportarEstructuraAsync(ImportacionEstructura importacion);
}
