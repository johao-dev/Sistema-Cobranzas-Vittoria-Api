using Cobranzas_Vittoria.Dtos.Requerimientos;
using Cobranzas_Vittoria.Entities;
using Cobranzas_Vittoria.Interfaces;

namespace Cobranzas_Vittoria.Repositories;

/// <summary>Adaptador interno al flujo normalizado, sin llamadas a SPs dbo inexistentes.</summary>
public class RequerimientosRepository(IRequerimientoRepository repository) : IRequerimientosRepository
{
    public Task<int> CrearAsync(RequerimientoCreateDto dto) => repository.CrearAsync(dto);

    public async Task<(Requerimiento? requerimiento, List<RequerimientoDetalle> items,
        List<RequerimientoValidacion> validaciones)> ObtenerAsync(int idRequerimiento)
    {
        var (head, items, validaciones) = await repository.GetAsync(idRequerimiento);
        return (head, items, validaciones);
    }
}
