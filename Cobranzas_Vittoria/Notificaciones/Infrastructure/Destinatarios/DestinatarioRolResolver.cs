using System.Data;
using System.Globalization;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Notificaciones.Application.Services;
using Cobranzas_Vittoria.Notificaciones.Domain.Model;
using Dapper;

namespace Cobranzas_Vittoria.Notificaciones.Infrastructure.Destinatarios;

public sealed class DestinatarioRolResolver(IDbConnectionFactory factory) : IDestinatarioResolver
{
    public TipoDestinatario Tipo => TipoDestinatario.Rol;

    public async Task<IReadOnlyCollection<DireccionCorreo>> ResolverAsync(
        ReglaDestinatario instruccion, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int id = int.Parse(instruccion.Valor!, CultureInfo.InvariantCulture);
        using IDbConnection db = factory.CreateConnection();

        IEnumerable<DireccionCorreo> direcciones = await db.QueryAsync<DireccionCorreo>(new CommandDefinition("""
            SELECT u.Correo, LTRIM(RTRIM(CONCAT(u.Nombres, N' ', u.Apellidos))) AS Nombre
            FROM seguridad.Usuario u
            WHERE u.Activo = 1 AND NULLIF(LTRIM(RTRIM(u.Correo)), N'') IS NOT NULL
                AND EXISTS (SELECT 1 FROM seguridad.UsuarioRol r WHERE r.IdUsuario = u.IdUsuario AND r.IdRol = @Id)
            ORDER BY u.IdUsuario;
            """, new { Id = id }, cancellationToken: cancellationToken));

        return direcciones.ToList().AsReadOnly();
    }
}
