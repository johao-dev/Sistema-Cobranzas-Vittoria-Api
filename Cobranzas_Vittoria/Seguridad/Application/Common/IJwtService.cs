using UsuarioDomain = Cobranzas_Vittoria.Seguridad.Domain.Model.Usuario;
using RolDomain = Cobranzas_Vittoria.Seguridad.Domain.Model.Rol;

namespace Cobranzas_Vittoria.Seguridad.Application.Common;

public interface IJwtService
{
    JwtToken GenerarToken(UsuarioDomain usuario, IEnumerable<RolDomain> roles);
}

// TODO: Este record podría ir en otro archivo, pero por ahora lo dejo aquí para no crear un archivo más.
public sealed record JwtToken(string Token, DateTime ExpiracionUtc);
