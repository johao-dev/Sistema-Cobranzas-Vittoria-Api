using System.Data;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Notificaciones.Application.RequerimientoCreado;
using Dapper;

namespace Cobranzas_Vittoria.Notificaciones.Infrastructure.Persistence;

/// <summary>Proyección de integración de solo lectura sobre el requerimiento confirmado.</summary>
public sealed class ConsultaRequerimientoCreado(IDbConnectionFactory factory) : IConsultaRequerimientoCreado
{
    public async Task<DatosRequerimientoCreado> ObtenerAsync(int idRequerimiento, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using IDbConnection db = factory.CreateConnection();

        DatosRequerimientoCreado? datos = await db.QuerySingleOrDefaultAsync<DatosRequerimientoCreado>(new CommandDefinition("""
            SELECT p.NombreProyecto AS Proyecto, LTRIM(RTRIM(CONCAT(u.Nombres, N' ', u.Apellidos))) AS Solicitante
            FROM compras.Requerimiento r
            JOIN maestra.Proyecto p ON p.IdProyecto = r.IdProyecto
            JOIN seguridad.Usuario u ON u.IdUsuario = r.IdUsuarioSolicitante
            WHERE r.IdRequerimiento = @idRequerimiento;
            """, new { idRequerimiento }, cancellationToken: cancellationToken));

        return datos ?? throw new InvalidOperationException("No se encontraron las referencias del requerimiento confirmado.");
    }
}
