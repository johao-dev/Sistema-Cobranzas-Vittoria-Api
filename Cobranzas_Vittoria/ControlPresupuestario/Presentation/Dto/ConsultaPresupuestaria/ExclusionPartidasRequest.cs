using System.ComponentModel.DataAnnotations;

namespace Cobranzas_Vittoria.ControlPresupuestario.Presentation.Dto.ConsultaPresupuestaria;

/// <summary>Exclusiones opcionales de la distribución del dashboard, recibidas como claves repetidas.</summary>
public sealed class ExclusionPartidasRequest : IValidatableObject
{
    public int[]? PartidasExcluidas { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (PartidasExcluidas?.Any(id => id <= 0) == true)
            yield return new ValidationResult("Las partidas excluidas deben tener identificadores positivos.",
                new[] { nameof(PartidasExcluidas) });
    }
}
