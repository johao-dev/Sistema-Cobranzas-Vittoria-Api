using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;

namespace Cobranzas_Vittoria.ControlPresupuestario.Domain.Model;

/// <summary>Centro de costo: un proyecto (relación 1:1 opcional) o un área corporativa.</summary>
public sealed class CentroCosto
{
    public int IdCentroCosto { get; private set; }
    public string Codigo { get; private set; } = string.Empty;
    public string Nombre { get; private set; } = string.Empty;
    public string? Descripcion { get; private set; }
    public int IdTipoCentroCosto { get; private set; }
    public string? CodigoTipoCentroCosto { get; private set; }
    public string? NombreTipoCentroCosto { get; private set; }
    public int? IdProyecto { get; private set; }
    public string? NombreProyecto { get; private set; }
    public bool Activo { get; private set; }
    public DateTime? FechaCreacion { get; private set; }
    public DateTime? FechaModificacion { get; private set; }

    private CentroCosto() { }

    public static CentroCosto Crear(string codigo, string nombre, int idTipoCentroCosto, string? descripcion, int? idProyecto)
        => new()
        {
            Codigo = Reglas.Requerido(codigo, "Codigo", 30),
            Nombre = Reglas.Requerido(nombre, "Nombre", 150),
            IdTipoCentroCosto = Reglas.Id(idTipoCentroCosto, "IdTipoCentroCosto"),
            Descripcion = Reglas.Opcional(descripcion, "Descripcion", 255),
            IdProyecto = Reglas.IdOpcional(idProyecto, "IdProyecto"),
            Activo = true
        };

    public static CentroCosto Reconstruir(int idCentroCosto, string codigo, string nombre, string? descripcion,
        int idTipoCentroCosto, string? codigoTipoCentroCosto, string? nombreTipoCentroCosto, int? idProyecto,
        string? nombreProyecto, bool activo, DateTime? fechaCreacion, DateTime? fechaModificacion)
        => new()
        {
            IdCentroCosto = idCentroCosto,
            Codigo = codigo,
            Nombre = nombre,
            Descripcion = descripcion,
            IdTipoCentroCosto = idTipoCentroCosto,
            CodigoTipoCentroCosto = codigoTipoCentroCosto,
            NombreTipoCentroCosto = nombreTipoCentroCosto,
            IdProyecto = idProyecto,
            NombreProyecto = nombreProyecto,
            Activo = activo,
            FechaCreacion = fechaCreacion,
            FechaModificacion = fechaModificacion
        };

    /// <summary>
    /// El código y el tipo identifican al centro de costo y no se editan. Se aceptan en la
    /// solicitud por contrato, pero deben coincidir con los actuales.
    /// </summary>
    public void Actualizar(string nombre, bool activo, string? descripcion, int? idProyecto,
        string? codigo = null, int? idTipoCentroCosto = null)
    {
        if (!string.IsNullOrWhiteSpace(codigo) && !string.Equals(codigo.Trim(), Codigo, StringComparison.OrdinalIgnoreCase))
            throw new ValidacionPresupuestariaException("CODIGO_NO_EDITABLE", "El código del centro de costo no se puede modificar.");
        if (idTipoCentroCosto is not null && idTipoCentroCosto != IdTipoCentroCosto)
            throw new ValidacionPresupuestariaException("TIPO_NO_EDITABLE", "El tipo del centro de costo no se puede modificar.");

        Nombre = Reglas.Requerido(nombre, "Nombre", 150);
        Activo = activo;
        Descripcion = Reglas.Opcional(descripcion, "Descripcion", 255);
        IdProyecto = Reglas.IdOpcional(idProyecto, "IdProyecto");
    }
}
