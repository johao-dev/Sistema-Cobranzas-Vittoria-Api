using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model;

namespace Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;


/// <summary>Puerto de persistencia de centros de costo.</summary>
public interface ICentroCostoRepository
{
    Task<IReadOnlyList<CentroCosto>> ListarAsync(bool? activo, int? idTipoCentroCosto, int? idProyecto, string? busqueda);
    Task<CentroCosto?> ObtenerAsync(int idCentroCosto);
    Task<int> CrearAsync(CentroCosto centroCosto);
    Task ActualizarAsync(CentroCosto centroCosto);
}
