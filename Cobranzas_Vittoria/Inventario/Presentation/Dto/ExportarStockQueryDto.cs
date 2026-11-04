using System.ComponentModel.DataAnnotations;

namespace Cobranzas_Vittoria.Inventario.Presentation.Dto;

public sealed class ExportarStockQueryDto : IValidatableObject
{
    [Range(1, int.MaxValue)]
    public int? IdProyecto { get; set; }

    [Range(1, int.MaxValue)]
    public int? IdEspecialidad { get; set; }

    public DateOnly? FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FechaDesde.HasValue && FechaHasta.HasValue && FechaDesde.Value > FechaHasta.Value)
        {
            yield return new ValidationResult(
                "La fecha hasta debe ser igual o posterior a la fecha desde.",
                new[] { nameof(FechaHasta) });
        }
    }
}
