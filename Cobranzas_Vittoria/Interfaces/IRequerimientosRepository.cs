using Cobranzas_Vittoria.Dtos.Requerimientos;
using Cobranzas_Vittoria.Entities;

namespace Cobranzas_Vittoria.Interfaces
{
    public interface IRequerimientosRepository
    {
        Task<int> CrearAsync(RequerimientoCreateDto dto);

        Task<(Requerimiento? requerimiento,
              List<RequerimientoDetalle> items,
              List<RequerimientoValidacion> validaciones)>
            ObtenerAsync(int idRequerimiento);
    }
}
