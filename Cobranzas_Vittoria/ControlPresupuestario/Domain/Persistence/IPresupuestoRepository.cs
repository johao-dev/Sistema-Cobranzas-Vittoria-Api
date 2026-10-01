using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model;

namespace Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;


/// <summary>Puerto de persistencia de presupuestos.</summary>
public interface IPresupuestoRepository
{
    Task<IReadOnlyList<Presupuesto>> ListarAsync(bool? activo, int? idCentroCosto, int? idMoneda, string? busqueda);
    Task<Presupuesto?> ObtenerAsync(int idPresupuesto);
    Task<PresupuestoCreado> CrearAsync(Presupuesto presupuesto, string usuario);
    Task ActualizarAsync(Presupuesto presupuesto);

    /// <summary>Movimientos presupuestales, gastos directos no anulados y líneas de requerimiento que usan sus partidas.</summary>
    Task<RegistrosAsociadosPresupuesto> ContarRegistrosAsociadosAsync(int idPresupuesto);
}

public sealed record RegistrosAsociadosPresupuesto(int Movimientos, int GastosDirectos, int LineasRequerimiento)
{
    public bool Alguno => Movimientos + GastosDirectos + LineasRequerimiento > 0;
}
