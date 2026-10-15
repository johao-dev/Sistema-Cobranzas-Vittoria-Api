using Cobranzas_Vittoria.Compras.Domain.Model;

namespace Cobranzas_Vittoria.Compras.Domain.Persistence;

public interface IRequerimientoRepository
{
    Task<Requerimiento?> ObtenerConItemsAsync(int idRequerimiento, CancellationToken cancellationToken = default);
    
    // Para agregar y actualizar: las escrituras deben volver a comprobar dentro de su transacción las reglas que dependen del estado actual de la DB.
    Task<Requerimiento> AgregarItemAsync(int idRequerimiento, ItemRequerimiento item, CancellationToken cancellationToken = default);
    Task<Requerimiento> ActualizarItemAsync(int idRequerimiento, ItemRequerimiento item, CancellationToken cancellationToken = default);
    Task EliminarItemAsync(int idRequerimiento, int idDetalle, CancellationToken cancellationToken = default);
}
