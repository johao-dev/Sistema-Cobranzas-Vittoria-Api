using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Seguridad.Domain.Model;
using Cobranzas_Vittoria.Repositories;
using Cobranzas_Vittoria.Seguridad.Domain.Persistence;
using System.Data;
using Dapper;
using Cobranzas_Vittoria.Seguridad.Infrastructure.Persistence.Entity;
using Cobranzas_Vittoria.Seguridad.Infrastructure.Persistence.Mapper;

namespace Cobranzas_Vittoria.Seguridad.Infrastructure.Persistence.Repository;

public class PermisoRepository : RepositoryBase, IPermisoRepository
{
    public PermisoRepository(IDbConnectionFactory factory) : base(factory) { }

    public async Task<Permiso?> GetByIdAsync(int idPermiso)
    {
        using IDbConnection db = Open();
        PermisoEntity? permisoEntity = await db.QueryFirstOrDefaultAsync<PermisoEntity>(
            "seguridad.usp_Permiso_GetById",
            new { IdPermiso = idPermiso },
            commandType: CommandType.StoredProcedure);

        return permisoEntity is null ? null : PermisoMapper.ToDomain(permisoEntity);
    }

    public async Task<IEnumerable<Permiso>> GetAllAsync(bool activo = true)
    {
        using IDbConnection db = Open();
        IEnumerable<PermisoEntity> permisosEntities = await db.QueryAsync<PermisoEntity>(
            "seguridad.usp_Permiso_List",
            new { Activo = activo },
            commandType: CommandType.StoredProcedure);

        return permisosEntities.Select(PermisoMapper.ToDomain);
    }

    public async Task<Permiso> UpdateAsync(Permiso permiso)
    {
        using IDbConnection db = Open();
        PermisoEntity entity = await db.QueryFirstAsync<PermisoEntity>(
            "seguridad.usp_Permiso_Update",
            new
            {
                permiso.IdPermiso,
                permiso.Nombre,
                permiso.Descripcion,
                permiso.Activo,
                permiso.FechaModificacion,
                permiso.UsuarioModificacion
            },
            commandType: CommandType.StoredProcedure);

        return PermisoMapper.ToDomain(entity);
    }

    // TODO: Implement EnableAsync and DisableAsync methods

    /*
        SQL parametrizado de referencia:

        UPDATE seguridad.Permiso
        SET Activo = @Activo,
            FechaModificacion = @FechaModificacion,
            UsuarioModificacion = @UsuarioModificacion
        WHERE IdPermiso = @IdPermiso;
    */
    public Task EnableAsync(int idPermiso)
    {
        throw new NotImplementedException();
    }

    public Task DisableAsync(int idPermiso)
    {
        throw new NotImplementedException();
    }
}
