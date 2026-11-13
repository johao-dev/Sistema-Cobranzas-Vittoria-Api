using System.Data;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Notificaciones.Application.PresupuestoAprobado;
using Dapper;

namespace Cobranzas_Vittoria.Notificaciones.Infrastructure.Persistence;

/// <summary>Proyección de integración de la versión confirmada, aunque ya sea histórica.</summary>
public sealed class ConsultaPresupuestoAprobado(IDbConnectionFactory factory) : IConsultaPresupuestoAprobado
{
    public async Task<DatosPresupuestoAprobado> ObtenerAsync(int idPresupuestoVersion, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using IDbConnection db = factory.CreateConnection();

        DatosPresupuestoAprobado? datos = await db.QuerySingleOrDefaultAsync<DatosPresupuestoAprobado>(new CommandDefinition("""
            SELECT p.Codigo AS CodigoPresupuesto, p.Nombre AS NombrePresupuesto,
                pro.NombreProyecto AS Proyecto, v.MotivoCambio
            FROM ControlPresupuestario.PresupuestoVersion v
            JOIN ControlPresupuestario.Presupuesto p ON p.IdPresupuesto = v.IdPresupuesto
            JOIN ControlPresupuestario.CentroCosto cc ON cc.IdCentroCosto = p.IdCentroCosto
            LEFT JOIN maestra.Proyecto pro ON pro.IdProyecto = cc.IdProyecto
            WHERE v.IdPresupuestoVersion = @idPresupuestoVersion;
            """, new { idPresupuestoVersion }, cancellationToken: cancellationToken));

        return datos ?? throw new InvalidOperationException("No se encontraron las referencias de la versión de presupuesto confirmada.");
    }
}
