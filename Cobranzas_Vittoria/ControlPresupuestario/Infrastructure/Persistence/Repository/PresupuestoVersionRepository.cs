using System.Data;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Repositories;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;
using Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Entity;
using Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Mapper;
using Dapper;

namespace Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Repository;

/// <summary>Adaptador Dapper de versiones sobre los SPs usp_PresupuestoVersion_*.</summary>
public sealed class PresupuestoVersionRepository : RepositoryBase, IPresupuestoVersionRepository
{
    private const string Schema = "ControlPresupuestario.";

    public PresupuestoVersionRepository(IDbConnectionFactory factory) : base(factory) { }

    public Task<IReadOnlyList<PresupuestoVersion>> ListarAsync(int idPresupuesto)
        => TraductorErroresSql.EjecutarAsync<IReadOnlyList<PresupuestoVersion>>(async () =>
    {
        using var db = Open();
        var filas = await db.QueryAsync<PresupuestoVersionEntity>(Schema + "usp_PresupuestoVersion_Listar",
            new { IdPresupuesto = idPresupuesto }, commandType: CommandType.StoredProcedure);
        return filas.Select(PresupuestoVersionMapper.ToDomain).ToList();
    });

    public Task<PresupuestoVersion?> ObtenerAsync(int idPresupuestoVersion) => TraductorErroresSql.EjecutarAsync(async () =>
    {
        using var db = Open();
        var fila = await db.QueryFirstOrDefaultAsync<PresupuestoVersionEntity>(Schema + "usp_PresupuestoVersion_Obtener",
            new { IdPresupuestoVersion = idPresupuestoVersion }, commandType: CommandType.StoredProcedure);
        return fila is null ? null : PresupuestoVersionMapper.ToDomain(fila);
    });

    public Task<VersionCreada> CrearNuevaAsync(int idPresupuesto, string? descripcion, string? motivoCambio, string usuario)
        => TraductorErroresSql.EjecutarAsync(async () =>
    {
        using var db = Open();
        var fila = await db.QueryFirstAsync<VersionCreadaEntity>(Schema + "usp_PresupuestoVersion_CrearNueva",
            new { IdPresupuesto = idPresupuesto, Descripcion = descripcion, MotivoCambio = motivoCambio, UsuarioCreacion = usuario },
            commandType: CommandType.StoredProcedure);
        return PresupuestoVersionMapper.ToDomain(fila);
    });

    public Task AprobarAsync(int idPresupuestoVersion, string usuario) => TraductorErroresSql.EjecutarAsync(async () =>
    {
        using var db = Open();
        await db.ExecuteAsync(Schema + "usp_PresupuestoVersion_Aprobar",
            new { IdPresupuestoVersion = idPresupuestoVersion, UsuarioAprobacion = usuario },
            commandType: CommandType.StoredProcedure);
    });

    public Task AnularAsync(int idPresupuestoVersion, string motivo, string usuario) => TraductorErroresSql.EjecutarAsync(async () =>
    {
        using var db = Open();
        await db.ExecuteAsync(Schema + "usp_PresupuestoVersion_Anular",
            new { IdPresupuestoVersion = idPresupuestoVersion, Motivo = motivo, UsuarioAnulacion = usuario },
            commandType: CommandType.StoredProcedure);
    });
}
