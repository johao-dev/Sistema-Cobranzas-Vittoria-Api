using Dapper;

namespace Cobranzas_Vittoria.Tests.Integration.Common;

internal static class IntegracionEconomicaTestData
{
    public static async Task<int> ObtenerOCrearDetalleAprobadoAsync(
        int idProyecto = SeedIds.ProyectoMaytaCapacII,
        decimal monto = 1_000_000m)
    {
        await using var cn = await DbHelpers.OpenTestConnectionAsync();
        using var tx = cn.BeginTransaction();

        await cn.ExecuteAsync("""
            INSERT INTO ControlPresupuestario.EstadoPresupuesto (Codigo, Nombre)
            SELECT Codigo, Codigo FROM (VALUES
                ('BORRADOR'), ('APROBADO'), ('HISTORICO'), ('ANULADO')) v(Codigo)
            WHERE NOT EXISTS (SELECT 1 FROM ControlPresupuestario.EstadoPresupuesto e WHERE e.Codigo = v.Codigo);

            INSERT INTO ControlPresupuestario.TipoMovimientoPresupuestal (Codigo, Nombre)
            SELECT Codigo, Codigo FROM (VALUES
                ('COMPROMISO'), ('LIBERACION'), ('EJECUCION'), ('AJUSTE')) v(Codigo)
            WHERE NOT EXISTS (SELECT 1 FROM ControlPresupuestario.TipoMovimientoPresupuestal t WHERE t.Codigo = v.Codigo);

            IF NOT EXISTS (SELECT 1 FROM ControlPresupuestario.TipoCentroCosto WHERE Codigo = 'PROYECTO')
                INSERT INTO ControlPresupuestario.TipoCentroCosto (Codigo, Nombre) VALUES ('PROYECTO', N'Proyecto');

            IF NOT EXISTS (SELECT 1 FROM ControlPresupuestario.TipoPartida WHERE Codigo = 'MATERIALES')
                INSERT INTO ControlPresupuestario.TipoPartida (Codigo, Nombre) VALUES ('MATERIALES', N'Materiales');
            """, transaction: tx);

        var existente = await cn.QueryFirstOrDefaultAsync<int?>("""
            SELECT TOP (1) pd.IdPresupuestoDetalle
            FROM ControlPresupuestario.PresupuestoDetalle pd
            JOIN ControlPresupuestario.PresupuestoVersion pv ON pv.IdPresupuestoVersion = pd.IdPresupuestoVersion
            JOIN ControlPresupuestario.EstadoPresupuesto ep ON ep.IdEstadoPresupuesto = pv.IdEstadoPresupuesto
            JOIN ControlPresupuestario.Presupuesto p ON p.IdPresupuesto = pv.IdPresupuesto
            JOIN ControlPresupuestario.CentroCosto cc ON cc.IdCentroCosto = p.IdCentroCosto
            JOIN maestra.Moneda m ON m.IdMoneda = p.IdMoneda
            WHERE cc.IdProyecto = @idProyecto AND ep.Codigo = 'APROBADO' AND m.Codigo = 'PEN'
            ORDER BY pd.IdPresupuestoDetalle;
            """, new { idProyecto }, tx);

        if (existente.HasValue)
        {
            tx.Commit();
            return existente.Value;
        }

        var sufijo = Guid.NewGuid().ToString("N")[..12];
        var idCentro = await cn.QuerySingleAsync<int>("""
            INSERT INTO ControlPresupuestario.CentroCosto
                (Codigo, Nombre, IdTipoCentroCosto, IdProyecto)
            VALUES (@codigo, N'Centro de proyecto para pruebas',
                (SELECT IdTipoCentroCosto FROM ControlPresupuestario.TipoCentroCosto WHERE Codigo = 'PROYECTO'),
                @idProyecto);
            SELECT CONVERT(INT, SCOPE_IDENTITY());
            """, new { codigo = $"CC-{sufijo}", idProyecto }, tx);

        var idPresupuesto = await cn.QuerySingleAsync<int>("""
            INSERT INTO ControlPresupuestario.Presupuesto
                (IdCentroCosto, IdMoneda, Codigo, Nombre)
            VALUES (@idCentro,
                (SELECT IdMoneda FROM maestra.Moneda WHERE Codigo = 'PEN'),
                @codigo, N'Presupuesto de pruebas');
            SELECT CONVERT(INT, SCOPE_IDENTITY());
            """, new { idCentro, codigo = $"PRE-{sufijo}" }, tx);

        var idVersion = await cn.QuerySingleAsync<int>("""
            INSERT INTO ControlPresupuestario.PresupuestoVersion
                (IdPresupuesto, IdEstadoPresupuesto, NumeroVersion, FechaAprobacion, UsuarioCreacion, UsuarioAprobacion)
            VALUES (@idPresupuesto,
                (SELECT IdEstadoPresupuesto FROM ControlPresupuestario.EstadoPresupuesto WHERE Codigo = 'APROBADO'),
                1, SYSUTCDATETIME(), N'test', N'test');
            SELECT CONVERT(INT, SCOPE_IDENTITY());
            """, new { idPresupuesto }, tx);

        var idPartida = await cn.QuerySingleAsync<int>("""
            INSERT INTO ControlPresupuestario.CatalogoPartida
                (Codigo, Nombre, IdTipoPartida, Nivel)
            VALUES (@codigo, N'Materiales de pruebas',
                (SELECT IdTipoPartida FROM ControlPresupuestario.TipoPartida WHERE Codigo = 'MATERIALES'), 1);
            SELECT CONVERT(INT, SCOPE_IDENTITY());
            """, new { codigo = $"MAT-{sufijo}" }, tx);

        var idDetalle = await cn.QuerySingleAsync<int>("""
            INSERT INTO ControlPresupuestario.PresupuestoDetalle
                (IdPresupuestoVersion, IdCatalogoPartida, MontoPresupuestado, Observacion)
            VALUES (@idVersion, @idPartida, @monto, N'Detalle para pruebas de integración económica');
            SELECT CONVERT(INT, SCOPE_IDENTITY());
            """, new { idVersion, idPartida, monto }, tx);

        tx.Commit();
        return idDetalle;
    }

    public static async Task AsignarDetalleARequerimientoAsync(int idRequerimiento, int idPresupuestoDetalle)
    {
        await using var cn = await DbHelpers.OpenTestConnectionAsync();
        await cn.ExecuteAsync("""
            UPDATE compras.RequerimientoDetalle
            SET IdPresupuestoDetalle = @idPresupuestoDetalle
            WHERE IdRequerimiento = @idRequerimiento;
            """, new { idRequerimiento, idPresupuestoDetalle });
    }
}
