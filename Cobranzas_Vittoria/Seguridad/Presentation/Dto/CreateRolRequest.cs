using System.ComponentModel.DataAnnotations;

namespace Cobranzas_Vittoria.Seguridad.Presentation.Dto;

public sealed record CreateRolRequest
(
    [Required(ErrorMessage = "El nombre es obligatorio")]
    string Nombre,

    string Descripcion
);
