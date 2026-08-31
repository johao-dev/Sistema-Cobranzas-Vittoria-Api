using System.ComponentModel.DataAnnotations;

namespace Cobranzas_Vittoria.Seguridad.Presentation.Dto;

public sealed record CreatePermisoRequest
(
    [Required(ErrorMessage = "El código es obligatorio")]
    string Codigo, // convencion: recurso.accion, ej: inventario.crear
    
    [Required(ErrorMessage = "El nombre es obligatorio")]
    string Nombre,
    
    string Descripcion
);