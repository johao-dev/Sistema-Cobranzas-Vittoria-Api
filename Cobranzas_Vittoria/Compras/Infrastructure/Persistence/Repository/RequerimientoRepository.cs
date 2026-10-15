using Cobranzas_Vittoria.Compras.Domain.Model;
using Cobranzas_Vittoria.Compras.Domain.Persistence;

namespace Cobranzas_Vittoria.Compras.Infrastructure.Persistence.Repository;

public class RequerimientoRepository : IRequerimientoRepository
{
    Task<Requerimiento> IRequerimientoRepository.ActualizarItemAsync(int idRequerimiento, ItemRequerimiento item, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    Task<Requerimiento> IRequerimientoRepository.AgregarItemAsync(int idRequerimiento, ItemRequerimiento item, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    Task IRequerimientoRepository.EliminarItemAsync(int idRequerimiento, int idDetalle, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    Task<Requerimiento?> IRequerimientoRepository.ObtenerConItemsAsync(int idRequerimiento, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}