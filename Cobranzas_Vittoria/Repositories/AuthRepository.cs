using System.Data;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Dtos.Auth;
using Cobranzas_Vittoria.Interfaces;
using Dapper;

namespace Cobranzas_Vittoria.Repositories;

public class AuthRepository : RepositoryBase, IAuthRepository
{
    public AuthRepository(IDbConnectionFactory factory) : base(factory) { }

    public async Task<LoginResponseDto?> LoginAsync(string usuarioLogin, string password)
    {
        using var db = Open();
        const string sql = @"
SELECT TOP 1
       u.IdUsuario,
       u.Nombres,
       u.Apellidos,
       u.Correo,
       u.UsuarioLogin,
       r.NombreRol
FROM seguridad.Usuario u
OUTER APPLY (
    SELECT TOP 1 r.NombreRol
    FROM seguridad.UsuarioRol ur
    INNER JOIN seguridad.Rol r ON r.IdRol = ur.IdRol
    WHERE ur.IdUsuario = u.IdUsuario
    ORDER BY ur.IdUsuarioRol DESC
) r
WHERE u.Activo = 1
  AND u.UsuarioLogin = @UsuarioLogin
  AND u.PasswordHash = @Password;";

        return await db.QueryFirstOrDefaultAsync<LoginResponseDto>(sql, new
        {
            UsuarioLogin = usuarioLogin,
            Password = password
        }, commandType: CommandType.Text);
    }
}
