using Cobranzas_Vittoria.Compras.Domain.Model;
using Cobranzas_Vittoria.Compras.Domain.ValueObject;

namespace Cobranzas_Vittoria.Compras.Domain.Persistence;

public interface IRequerimientoRepository
{
    Task<Requerimiento?> ObtenerConItemsAsync(int idRequerimiento, CancellationToken cancellationToken = default);
    // La escritura vuelve a comprobar estado, pertenencia y referencias dentro de su transacción.
    Task<ItemRequerimiento> AgregarItemAsync(int idRequerimiento, ItemRequerimiento item, CancellationToken cancellationToken = default);
    Task<ItemRequerimiento> ActualizarItemAsync(int idRequerimiento, ItemRequerimiento item,
        CamposItemRequerimiento campos, CancellationToken cancellationToken = default);
    Task EliminarItemAsync(int idRequerimiento, int idDetalle, CancellationToken cancellationToken = default);
}
