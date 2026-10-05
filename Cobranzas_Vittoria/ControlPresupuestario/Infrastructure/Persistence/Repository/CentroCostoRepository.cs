using System.Data;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Repositories;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;
using Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Entity;
using Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Mapper;
using Dapper;

namespace Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Repository;

/// <summary>Adaptador Dapper de centros de costo sobre los SPs usp_CentroCosto_*.</summary>
public sealed class CentroCostoRepository : RepositoryBase, ICentroCostoRepository
{
    private const string Schema = "ControlPresupuestario.";

    public CentroCostoRepository(IDbConnectionFactory factory) : base(factory) { }

    public Task<IReadOnlyList<CentroCosto>> ListarAsync(bool? activo, int? idTipoCentroCosto, int? idProyecto,
        string? busqueda) => TraductorErroresSql.EjecutarAsync<IReadOnlyList<CentroCosto>>(async () =>
    {
        using var db = Open();
        var filas = await db.QueryAsync<CentroCostoEntity>(Schema + "usp_CentroCosto_Listar",
            new { Activo = activo, IdTipoCentroCosto = idTipoCentroCosto, Busqueda = busqueda },
            commandType: CommandType.StoredProcedure);
        // El SP no filtra por proyecto; el filtro del contrato se aplica sobre su resultado.
        return filas.Where(f => idProyecto is null || f.IdProyecto == idProyecto)
            .Select(CentroCostoMapper.ToDomain).ToList();
    });

    public Task<CentroCosto?> ObtenerAsync(int idCentroCosto) => TraductorErroresSql.EjecutarAsync(async () =>
    {
        using var db = Open();
        var fila = await db.QueryFirstOrDefaultAsync<CentroCostoEntity>(Schema + "usp_CentroCosto_Obtener",
            new { IdCentroCosto = idCentroCosto }, commandType: CommandType.StoredProcedure);
        return fila is null ? null : CentroCostoMapper.ToDomain(fila);
    });

    public Task<int> CrearAsync(CentroCosto c) => TraductorErroresSql.EjecutarAsync(async () =>
    {
        using var db = Open();
        return await db.ExecuteScalarAsync<int>(Schema + "usp_CentroCosto_Crear",
            new { c.Codigo, c.Nombre, c.IdTipoCentroCosto, c.Descripcion, c.IdProyecto },
            commandType: CommandType.StoredProcedure);
    });

    public Task ActualizarAsync(CentroCosto c) => TraductorErroresSql.EjecutarAsync(async () =>
    {
        using var db = Open();
        await db.ExecuteAsync(Schema + "usp_CentroCosto_Actualizar",
            new { c.IdCentroCosto, c.Nombre, c.Activo, c.Descripcion, c.IdProyecto },
            commandType: CommandType.StoredProcedure);
    });
}
