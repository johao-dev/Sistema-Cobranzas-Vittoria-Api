using System.Data;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Repositories;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;
using Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Entity;
using Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Mapper;
using Dapper;

namespace Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Repository;

/// <summary>Adaptador Dapper del catálogo de partidas sobre los SPs usp_CatalogoPartida_*.</summary>
public sealed class CatalogoPartidaRepository : RepositoryBase, ICatalogoPartidaRepository
{
    private const string Schema = "ControlPresupuestario.";

    public CatalogoPartidaRepository(IDbConnectionFactory factory) : base(factory) { }

    public Task<IReadOnlyList<CatalogoPartida>> ListarAsync(FiltroPartidas f)
        => TraductorErroresSql.EjecutarAsync<IReadOnlyList<CatalogoPartida>>(async () =>
    {
        using var db = Open();
        var filas = await db.QueryAsync<CatalogoPartidaEntity>(Schema + "usp_CatalogoPartida_Listar",
            new
            {
                f.Activo, f.IdTipoPartida, f.IdPartidaPadre, f.SoloRaices, f.EsHoja, f.Busqueda, f.IdSeccionGasto
            }, commandType: CommandType.StoredProcedure);
        return filas.Select(CatalogoPartidaMapper.ToDomain).ToList();
    });

    public Task<CatalogoPartida?> ObtenerAsync(int idCatalogoPartida) => TraductorErroresSql.EjecutarAsync(async () =>
    {
        using var db = Open();
        var fila = await db.QueryFirstOrDefaultAsync<CatalogoPartidaEntity>(Schema + "usp_CatalogoPartida_Obtener",
            new { IdCatalogoPartida = idCatalogoPartida }, commandType: CommandType.StoredProcedure);
        return fila is null ? null : CatalogoPartidaMapper.ToDomain(fila);
    });

    public Task<int> CrearAsync(CatalogoPartida p) => TraductorErroresSql.EjecutarAsync(async () =>
    {
        using var db = Open();
        return await db.ExecuteScalarAsync<int>(Schema + "usp_CatalogoPartida_Crear",
            new { p.Codigo, p.Nombre, p.IdTipoPartida, p.IdPartidaPadre, p.Descripcion, p.IdSeccionGasto },
            commandType: CommandType.StoredProcedure);
    });

    public Task ActualizarAsync(CatalogoPartida p) => TraductorErroresSql.EjecutarAsync(async () =>
    {
        using var db = Open();
        await db.ExecuteAsync(Schema + "usp_CatalogoPartida_Actualizar",
            new { p.IdCatalogoPartida, p.Nombre, p.IdTipoPartida, p.Activo, p.IdPartidaPadre, p.Descripcion, p.IdSeccionGasto },
            commandType: CommandType.StoredProcedure);
    });
}
