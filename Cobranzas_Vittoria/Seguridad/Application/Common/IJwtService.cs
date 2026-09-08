using UsuarioDomain = Cobranzas_Vittoria.Seguridad.Domain.Model.Usuario;
using RolDomain = Cobranzas_Vittoria.Seguridad.Domain.Model.Rol;

namespace Cobranzas_Vittoria.Seguridad.Application.Common;

public interface IJwtService
{
    JwtToken GenerarToken(UsuarioDomain usuario, IEnumerable<RolDomain> roles);
}
