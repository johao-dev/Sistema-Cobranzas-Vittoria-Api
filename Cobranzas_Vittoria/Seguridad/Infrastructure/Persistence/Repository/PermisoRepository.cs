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
        PermisoEntity? permisoEntity = await db.QueryFirstOrDefaultAsync<PermisoEntity>
            ("SELECT * FROM Permiso WHERE IdPermiso = @IdPermiso", new { IdPermiso = idPermiso });
        return permisoEntity is null ? null : PermisoMapper.ToDomain(permisoEntity); // TODO: Reemplazar retorno null por excepcion
    }

    public async Task<IEnumerable<Permiso>> GetAllAsync(bool activo = true)
    {
        using IDbConnection db = Open();
        IEnumerable<PermisoEntity> permisosEntities = await db.QueryAsync<PermisoEntity>
            ("SELECT * FROM Permiso WHERE Activo = @Activo", new { Activo = activo });
        return permisosEntities.Select(PermisoMapper.ToDomain);
    }

    public async Task<Permiso> AddAsync(Permiso permiso)
    {
        using IDbConnection db = Open();
        PermisoEntity entity = await db.QueryFirstAsync<PermisoEntity>(
            "seguridad.usp_Permiso_Insert",
            new { permiso.Codigo, permiso.Nombre, permiso.Descripcion },
            commandType: CommandType.StoredProcedure
        );

        return PermisoMapper.ToDomain(entity);
    }

    public async Task<Permiso> UpdateAsync(Permiso permiso)
    {
        using IDbConnection db = Open();
        PermisoEntity permisoEntity = PermisoMapper.ToEntity(permiso);
        await db.ExecuteAsync(
            "UPDATE Permiso SET Codigo = @Codigo, Nombre = @Nombre, Descripcion = @Descripcion, Activo = @Activo, FechaModificacion = @FechaModificacion, UsuarioModificacion = @UsuarioModificacion " +
            "WHERE IdPermiso = @IdPermiso",
            new
            {
                IdPermiso = permisoEntity.IdPermiso,
                Codigo = permisoEntity.Codigo,
                Nombre = permisoEntity.Nombre,
                Descripcion = permisoEntity.Descripcion,
                Activo = permisoEntity.Activo,
                FechaModificacion = permisoEntity.FechaModificacion,
                UsuarioModificacion = permisoEntity.UsuarioModificacion
            });

        return permiso;
    }

    public async Task DeleteAsync(int idPermiso)
    {
        using IDbConnection db = Open();
        await db.ExecuteAsync(
            "DELETE FROM Permiso WHERE IdPermiso = @IdPermiso",
            new { IdPermiso = idPermiso });
    }
}
