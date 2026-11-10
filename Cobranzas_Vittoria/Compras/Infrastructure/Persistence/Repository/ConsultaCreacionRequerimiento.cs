using System.Data;
using Cobranzas_Vittoria.Compras.Application.Requerimiento.Crear;
using Cobranzas_Vittoria.Data;
using Dapper;

namespace Cobranzas_Vittoria.Compras.Infrastructure.Persistence.Repository;

public sealed class ConsultaCreacionRequerimiento(IDbConnectionFactory factory) : IConsultaCreacionRequerimiento
{
    public async Task<ContextoCreacionRequerimiento> ObtenerContextoAsync(int idProyecto, int idSolicitante,
        IReadOnlyCollection<int> materiales, IReadOnlyCollection<int> partidas, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using IDbConnection db = factory.CreateConnection();

        const string sql = """
            SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM maestra.Proyecto WHERE IdProyecto=@idProyecto) THEN 1 ELSE 0 END AS bit);
            SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM seguridad.Usuario WHERE IdUsuario=@idSolicitante) THEN 1 ELSE 0 END AS bit);
            SELECT IdMaterial FROM maestra.Material WHERE IdMaterial IN @materiales;
            SELECT pd.IdPresupuestoDetalle, ep.Codigo AS EstadoPresupuesto,
                p.Activo AS PresupuestoActivo, cc.Activo AS CentroCostoActivo, cc.IdProyecto
            FROM ControlPresupuestario.PresupuestoDetalle pd
            JOIN ControlPresupuestario.PresupuestoVersion pv ON pv.IdPresupuestoVersion=pd.IdPresupuestoVersion
            JOIN ControlPresupuestario.EstadoPresupuesto ep ON ep.IdEstadoPresupuesto=pv.IdEstadoPresupuesto
            JOIN ControlPresupuestario.Presupuesto p ON p.IdPresupuesto=pv.IdPresupuesto
            JOIN ControlPresupuestario.CentroCosto cc ON cc.IdCentroCosto=p.IdCentroCosto
            WHERE pd.IdPresupuestoDetalle IN @partidas;
            """;

        using SqlMapper.GridReader filas = await db.QueryMultipleAsync(new CommandDefinition(sql,
            new { idProyecto, idSolicitante, materiales, partidas }, cancellationToken: ct));

        bool existeProyecto = await filas.ReadSingleAsync<bool>();
        bool existeSolicitante = await filas.ReadSingleAsync<bool>();
        HashSet<int> materialesExistentes = (await filas.ReadAsync<int>()).ToHashSet();
        List<PartidaCreacionRequerimiento> partidasExistentes = (await filas.ReadAsync<PartidaCreacionRequerimiento>()).AsList();

        return new ContextoCreacionRequerimiento(existeProyecto, existeSolicitante, materialesExistentes, partidasExistentes);
    }

    public async Task<DisponibilidadNumeroRequerimiento> ObtenerNumeracionAsync(string numeroSolicitado, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using IDbConnection db = factory.CreateConnection();

        const string sql = """
            SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM compras.Requerimiento WHERE NumeroRequerimiento=@numeroSolicitado)
                THEN 1 ELSE 0 END AS bit);
            SELECT ISNULL(MAX(CONVERT(bigint, TRY_CONVERT(int, NumeroRequerimiento))), 0) FROM compras.Requerimiento;
            """;

        using SqlMapper.GridReader filas = await db.QueryMultipleAsync(new CommandDefinition(sql,
            new { numeroSolicitado }, cancellationToken: ct));

        bool existe = await filas.ReadSingleAsync<bool>();
        long maximo = await filas.ReadSingleAsync<long>();
        return new DisponibilidadNumeroRequerimiento(existe, maximo);
    }
}
