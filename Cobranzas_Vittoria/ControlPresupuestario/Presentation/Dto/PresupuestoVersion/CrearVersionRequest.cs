using System.ComponentModel.DataAnnotations;

namespace Cobranzas_Vittoria.ControlPresupuestario.Presentation.Dto.PresupuestoVersion;

/// <summary>El cuerpo puede ir vacío ({}): el SP copia el snapshot de la versión aprobada vigente.</summary>
public sealed class CrearVersionRequest
{
    [StringLength(500)]
    public string? Descripcion { get; set; }

    [StringLength(500)]
    public string? MotivoCambio { get; set; }
}
