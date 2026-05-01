using System;
using System.ComponentModel.DataAnnotations;

namespace Vittoria.Api.Models;

public sealed class GastoAdministrativoDto
{
    public int IdGastoAdministrativo { get; set; }
    public int? IdProyecto { get; set; }
    public string? Proyecto { get; set; }
    public DateTime FechaEmision { get; set; }
    public string? Proveedor { get; set; }
    public string Concepto { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string Moneda { get; set; } = "PEN";
    public decimal Total { get; set; }
    public string? RutaArchivo { get; set; }
    public bool Activo { get; set; } = true;
    public string? UsuarioCreacion { get; set; }
    public DateTime? FechaCreacion { get; set; }
    public string? UsuarioModificacion { get; set; }
    public DateTime? FechaModificacion { get; set; }
}

public sealed class GuardarGastoAdministrativoRequest
{
    public int IdGastoAdministrativo { get; set; }

    [Required(ErrorMessage = "Debe seleccionar un proyecto.")]
    [Range(1, int.MaxValue, ErrorMessage = "Debe seleccionar un proyecto válido.")]
    public int IdProyecto { get; set; }

    [Required(ErrorMessage = "Debe ingresar la fecha de emisión.")]
    public DateTime FechaEmision { get; set; }

    [MaxLength(200)]
    public string? Proveedor { get; set; }

    [Required(ErrorMessage = "Debe ingresar el concepto.")]
    [MaxLength(200)]
    public string Concepto { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Descripcion { get; set; }

    [Required]
    [MaxLength(10)]
    public string Moneda { get; set; } = "PEN";

    [Range(0, double.MaxValue, ErrorMessage = "El total debe ser mayor o igual a cero.")]
    public decimal Total { get; set; }

    [MaxLength(500)]
    public string? RutaArchivo { get; set; }

    [MaxLength(100)]
    public string? Usuario { get; set; }
}
