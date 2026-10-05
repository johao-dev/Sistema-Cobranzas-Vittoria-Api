using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;

namespace Cobranzas_Vittoria.ControlPresupuestario.Domain.Model;

/// <summary>
/// Presupuesto de un centro de costo en una única moneda. Su contenido económico vive en
/// versiones (snapshots): al crearlo nace la versión 1 en BORRADOR.
/// </summary>
public sealed class Presupuesto
{
    public int IdPresupuesto { get; private set; }
    public string Codigo { get; private set; } = string.Empty;
    public string Nombre { get; private set; } = string.Empty;
    public string? Descripcion { get; private set; }
    public int IdCentroCosto { get; private set; }
    public string? CodigoCentroCosto { get; private set; }
    public string? NombreCentroCosto { get; private set; }
    public int IdMoneda { get; private set; }
    public string? CodigoMoneda { get; private set; }
    public string? NombreMoneda { get; private set; }
    public string? SimboloMoneda { get; private set; }
    public DateTime? FechaInicio { get; private set; }
    public DateTime? FechaFin { get; private set; }
    public bool Activo { get; private set; }
    public DateTime? FechaCreacion { get; private set; }
    public DateTime? FechaModificacion { get; private set; }
    public int? IdPresupuestoVersionAprobada { get; private set; }
    public int? IdPresupuestoVersionBorrador { get; private set; }
    public int CantidadAprobadas { get; private set; }
    public int CantidadBorradores { get; private set; }

    /// <summary>APROBADO, EN_ELABORACION, EN_REVISION, SIN_VERSION_VIGENTE o INCONSISTENTE.</summary>
    public string? EstadoElaboracion { get; private set; }

    public bool TieneVersionAprobada => IdPresupuestoVersionAprobada is not null;

    private Presupuesto() { }

    public static Presupuesto Crear(int idCentroCosto, int idMoneda, string codigo, string nombre, string? descripcion,
        DateTime? fechaInicio, DateTime? fechaFin)
    {
        ValidarRango(fechaInicio, fechaFin);
        return new Presupuesto
        {
            IdCentroCosto = Reglas.Id(idCentroCosto, "IdCentroCosto"),
            IdMoneda = Reglas.Id(idMoneda, "IdMoneda"),
            Codigo = Reglas.Requerido(codigo, "Codigo", 50),
            Nombre = Reglas.Requerido(nombre, "Nombre", 200),
            Descripcion = Reglas.Opcional(descripcion, "Descripcion", 500),
            FechaInicio = fechaInicio?.Date,
            FechaFin = fechaFin?.Date,
            Activo = true
        };
    }

    public static Presupuesto Reconstruir(int idPresupuesto, string codigo, string nombre, string? descripcion,
        int idCentroCosto, string? codigoCentroCosto, string? nombreCentroCosto, int idMoneda, string? codigoMoneda,
        string? nombreMoneda, string? simboloMoneda, DateTime? fechaInicio, DateTime? fechaFin, bool activo,
        DateTime? fechaCreacion, DateTime? fechaModificacion, int? idPresupuestoVersionAprobada,
        int? idPresupuestoVersionBorrador, int cantidadAprobadas, int cantidadBorradores, string? estadoElaboracion)
        => new()
        {
            IdPresupuesto = idPresupuesto,
            Codigo = codigo,
            Nombre = nombre,
            Descripcion = descripcion,
            IdCentroCosto = idCentroCosto,
            CodigoCentroCosto = codigoCentroCosto,
            NombreCentroCosto = nombreCentroCosto,
            IdMoneda = idMoneda,
            CodigoMoneda = codigoMoneda,
            NombreMoneda = nombreMoneda,
            SimboloMoneda = simboloMoneda,
            FechaInicio = fechaInicio,
            FechaFin = fechaFin,
            Activo = activo,
            FechaCreacion = fechaCreacion,
            FechaModificacion = fechaModificacion,
            IdPresupuestoVersionAprobada = idPresupuestoVersionAprobada,
            IdPresupuestoVersionBorrador = idPresupuestoVersionBorrador,
            CantidadAprobadas = cantidadAprobadas,
            CantidadBorradores = cantidadBorradores,
            EstadoElaboracion = estadoElaboracion
        };

    /// <summary>La moneda y el centro de costo no se editan: definen la historia económica del presupuesto.</summary>
    public void Actualizar(string nombre, bool activo, string? descripcion, DateTime? fechaInicio, DateTime? fechaFin)
    {
        ValidarRango(fechaInicio, fechaFin);
        Nombre = Reglas.Requerido(nombre, "Nombre", 200);
        Activo = activo;
        Descripcion = Reglas.Opcional(descripcion, "Descripcion", 500);
        FechaInicio = fechaInicio?.Date;
        FechaFin = fechaFin?.Date;
    }

    private static void ValidarRango(DateTime? inicio, DateTime? fin)
    {
        if (inicio.HasValue && fin.HasValue && fin.Value.Date < inicio.Value.Date)
            throw new ValidacionPresupuestariaException("RANGO_FECHAS_INVALIDO",
                "La fecha fin no puede ser anterior a la fecha inicio.");
    }
}

/// <summary>Resultado de crear un presupuesto: el SP crea en la misma transacción su versión 1 en BORRADOR.</summary>
public sealed record PresupuestoCreado(int IdPresupuesto, int IdPresupuestoVersion, int NumeroVersion, string Estado);
