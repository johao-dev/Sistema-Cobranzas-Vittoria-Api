using Cobranzas_Vittoria.ControlPresupuestario.Domain.ValueObject;

namespace Cobranzas_Vittoria.ControlPresupuestario.Domain.Model;

/// <summary>
/// Versión (snapshot) de un presupuesto. Las transiciones BORRADOR → APROBADO (la anterior
/// pasa a HISTORICO) y BORRADOR → ANULADO las garantiza SQL Server de forma atómica.
/// </summary>
public sealed class PresupuestoVersion
{
    public int IdPresupuestoVersion { get; private set; }
    public int IdPresupuesto { get; private set; }
    public int NumeroVersion { get; private set; }
    public int IdEstadoPresupuesto { get; private set; }
    public string Estado { get; private set; } = string.Empty;
    public string? NombreEstado { get; private set; }
    public string? Descripcion { get; private set; }
    public string? MotivoCambio { get; private set; }
    public string? MotivoAnulacion { get; private set; }
    public DateTime? FechaCreacion { get; private set; }
    public DateTime? FechaAprobacion { get; private set; }
    public DateTime? FechaAnulacion { get; private set; }
    public string? UsuarioCreacion { get; private set; }
    public string? UsuarioAprobacion { get; private set; }
    public string? UsuarioAnulacion { get; private set; }
    public string? CodigoPresupuesto { get; private set; }
    public string? NombrePresupuesto { get; private set; }
    public int IdCentroCosto { get; private set; }
    public int IdMoneda { get; private set; }
    public string? CodigoMoneda { get; private set; }
    public string? SimboloMoneda { get; private set; }

    public bool EsBorrador => Estado == EstadoPresupuesto.Borrador;

    private PresupuestoVersion() { }

    public static PresupuestoVersion Reconstruir(int idPresupuestoVersion, int idPresupuesto, int numeroVersion,
        int idEstadoPresupuesto, string estado, string? nombreEstado, string? descripcion, string? motivoCambio,
        string? motivoAnulacion, DateTime? fechaCreacion, DateTime? fechaAprobacion, DateTime? fechaAnulacion,
        string? usuarioCreacion, string? usuarioAprobacion, string? usuarioAnulacion, string? codigoPresupuesto,
        string? nombrePresupuesto, int idCentroCosto, int idMoneda, string? codigoMoneda, string? simboloMoneda)
        => new()
        {
            IdPresupuestoVersion = idPresupuestoVersion,
            IdPresupuesto = idPresupuesto,
            NumeroVersion = numeroVersion,
            IdEstadoPresupuesto = idEstadoPresupuesto,
            Estado = estado,
            NombreEstado = nombreEstado,
            Descripcion = descripcion,
            MotivoCambio = motivoCambio,
            MotivoAnulacion = motivoAnulacion,
            FechaCreacion = fechaCreacion,
            FechaAprobacion = fechaAprobacion,
            FechaAnulacion = fechaAnulacion,
            UsuarioCreacion = usuarioCreacion,
            UsuarioAprobacion = usuarioAprobacion,
            UsuarioAnulacion = usuarioAnulacion,
            CodigoPresupuesto = codigoPresupuesto,
            NombrePresupuesto = nombrePresupuesto,
            IdCentroCosto = idCentroCosto,
            IdMoneda = idMoneda,
            CodigoMoneda = codigoMoneda,
            SimboloMoneda = simboloMoneda
        };

    /// <summary>Una versión solo se expone como recurso subordinado de su propio presupuesto.</summary>
    public bool PerteneceA(int idPresupuesto) => IdPresupuesto == idPresupuesto;
}

/// <summary>Resultado de crear una versión nueva: el SP copia el snapshot de la versión APROBADA vigente.</summary>
public sealed record VersionCreada(int IdPresupuesto, int IdPresupuestoVersion, int NumeroVersion, string Estado,
    int? IdPresupuestoVersionBase, int CantidadDetallesCopiados);
