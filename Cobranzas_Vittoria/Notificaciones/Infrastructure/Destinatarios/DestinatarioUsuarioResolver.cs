using System.Data;
using System.Globalization;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Notificaciones.Application.Services;
using Cobranzas_Vittoria.Notificaciones.Domain.Model;
using Dapper;

namespace Cobranzas_Vittoria.Notificaciones.Infrastructure.Destinatarios;

public sealed class DestinatarioUsuarioResolver(IDbConnectionFactory factory) : IDestinatarioResolver
{
    public TipoDestinatario Tipo => TipoDestinatario.Usuario;

    public async Task<IReadOnlyCollection<DireccionCorreo>> ResolverAsync(
        ReglaDestinatario instruccion, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int id = int.Parse(instruccion.Valor!, CultureInfo.InvariantCulture);
        using IDbConnection db = factory.CreateConnection();

        IEnumerable<DireccionCorreo> direcciones = await db.QueryAsync<DireccionCorreo>(new CommandDefinition("""
            SELECT Correo, LTRIM(RTRIM(CONCAT(Nombres, N' ', Apellidos))) AS Nombre
            FROM seguridad.Usuario WHERE IdUsuario = @Id AND Activo = 1
                AND NULLIF(LTRIM(RTRIM(Correo)), N'') IS NOT NULL;
            """, new { Id = id }, cancellationToken: cancellationToken));

        return direcciones.ToList().AsReadOnly();
    }
}
