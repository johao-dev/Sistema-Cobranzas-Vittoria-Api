using System.ComponentModel.DataAnnotations;
using Cobranzas_Vittoria.Compras.Application.Requerimiento.Crear;

namespace Cobranzas_Vittoria.Compras.Presentation.Dto;

public sealed class CrearRequerimientoRequest
{
    [StringLength(30)]
    public string? NumeroRequerimiento { get; set; } = string.Empty;

    public DateTime FechaRequerimiento { get; set; }

    [Range(1, int.MaxValue)]
    public int IdProyecto { get; set; }

    [StringLength(250)]
    public string? Descripcion { get; set; }

    public DateTime? FechaEntrega { get; set; }

    // Se admite el campo del contrato anterior; la identidad efectiva se obtiene en el handler.
    public int IdUsuarioSolicitante { get; set; }

    [StringLength(250)]
    public string? Observacion { get; set; }

    [Required, MinLength(1)]
    public List<CrearItemRequerimientoRequest?>? Items { get; set; } = new();

    public CrearRequerimientoCommand ToCommand() => new(NumeroRequerimiento, DateOnly.FromDateTime(FechaRequerimiento),
        IdProyecto, Descripcion, FechaEntrega.HasValue ? DateOnly.FromDateTime(FechaEntrega.Value) : null,
        Observacion, Items?.Select(i => i is null ? null : new CrearItemRequerimientoCommand(
            i.IdMaterial, i.IdPresupuestoDetalle, i.Cantidad, i.Observacion)).ToArray());
}

public sealed class CrearItemRequerimientoRequest
{
    [Range(1, int.MaxValue)]
    public int IdMaterial { get; set; }

    [Range(1, int.MaxValue)]
    public int? IdPresupuestoDetalle { get; set; }

    public decimal Cantidad { get; set; }

    [StringLength(250)]
    public string? Observacion { get; set; }
}
