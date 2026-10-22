using System.ComponentModel.DataAnnotations;

namespace Cobranzas_Vittoria.Inventario.Presentation.Dto;

public sealed class TransferenciaProyectoCreateDto
{
    [Range(1, int.MaxValue)]
    public int IdProyectoOrigen { get; set; }

    [Range(1, int.MaxValue)]
    public int IdProyectoDestino { get; set; }

    [Required]
    public DateOnly? Fecha { get; set; }

    [Required(AllowEmptyStrings = true), StringLength(500)]
    public string? Observacion { get; set; }

    [Required, MinLength(1)]
    public List<TransferenciaProyectoItemDto> Items { get; set; } = new();
}

public sealed class TransferenciaProyectoItemDto
{
    [Range(1, int.MaxValue)]
    public int IdMaterial { get; set; }

    [Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal Cantidad { get; set; }
}
