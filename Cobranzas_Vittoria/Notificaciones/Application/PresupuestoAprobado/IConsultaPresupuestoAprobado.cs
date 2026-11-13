namespace Cobranzas_Vittoria.Notificaciones.Application.PresupuestoAprobado;

public interface IConsultaPresupuestoAprobado
{
    Task<DatosPresupuestoAprobado> ObtenerAsync(int idPresupuestoVersion, CancellationToken cancellationToken = default);
}

public sealed record DatosPresupuestoAprobado(
    string CodigoPresupuesto, string NombrePresupuesto, string? Proyecto, string? MotivoCambio);
