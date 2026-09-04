using System.ComponentModel.DataAnnotations;

namespace Cobranzas_Vittoria.Seguridad.Presentation.Dto;

public sealed record CreateUsuarioRequest
(
    [Required(ErrorMessage = "El nombre es obligatorio")]
    string Nombres,

    [Required(ErrorMessage = "El apellido es obligatorio")]
    string Apellidos,

    [Required(ErrorMessage = "El correo es obligatorio")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato valido")]
    string Correo,

    [Required(ErrorMessage = "El usuario de login es obligatorio")]
    string UsuarioLogin,

    [Required(ErrorMessage = "La contraseña es obligatoria")]
    string PasswordHash
);
