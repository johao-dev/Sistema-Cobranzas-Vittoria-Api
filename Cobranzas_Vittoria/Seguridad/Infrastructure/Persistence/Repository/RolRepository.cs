using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Repositories;
using Cobranzas_Vittoria.Seguridad.Domain.Model;
using Cobranzas_Vittoria.Seguridad.Domain.Persistence;
using Cobranzas_Vittoria.Seguridad.Infrastructure.Persistence.Entity;
using Cobranzas_Vittoria.Seguridad.Infrastructure.Persistence.Mapper;
using System.Data;
using Dapper;

namespace Cobranzas_Vittoria.Seguridad.Infrastructure.Persistence.Repository;

public class RolRepository : RepositoryBase, IRolRepository
{
    public RolRepository(IDbConnectionFactory factory) : base(factory) { }

    public async Task<Rol?> GetByIdAsync(int idRol)
    {
        using IDbConnection db = Open();
        RolEntity? rolEntity = await db.QueryFirstOrDefaultAsync<RolEntity>(
            "seguridad.usp_Rol_GetById",
            new { IdRol = idRol },
            commandType: CommandType.StoredProcedure);

        return rolEntity is null ? null : RolMapper.ToDomain(rolEntity);
    }

    public async Task<Rol?> GetByNombreAsync(string nombre)
    {
        using IDbConnection db = Open();
        RolEntity? rolEntity = await db.QueryFirstOrDefaultAsync<RolEntity>(
            "seguridad.usp_Rol_GetByNombre",
            new { Nombre = nombre },
            commandType: CommandType.StoredProcedure);

        return rolEntity is null ? null : RolMapper.ToDomain(rolEntity);
    }

    public async Task<IEnumerable<Rol>> GetAllAsync(bool? activo = true)
    {
        using IDbConnection db = Open();
        IEnumerable<RolEntity> rolesEntities = await db.QueryAsync<RolEntity>(
            "seguridad.usp_Rol_List",
            new { Activo = activo },
            commandType: CommandType.StoredProcedure);

        return rolesEntities.Select(RolMapper.ToDomain);
    }

    public async Task<Rol> AddAsync(Rol rol)
    {
        using IDbConnection db = Open();
        RolEntity entity = await db.QueryFirstAsync<RolEntity>(
            "seguridad.usp_Rol_Insert",
            new
            {
                rol.Nombre,
                rol.Descripcion,
                rol.Activo,
                rol.UsuarioCreacion
            },
            commandType: CommandType.StoredProcedure);

        return RolMapper.ToDomain(entity);
    }

    public async Task<Rol> UpdateAsync(Rol rol)
    {
        using IDbConnection db = Open();
        RolEntity entity = await db.QueryFirstAsync<RolEntity>(
            "seguridad.usp_Rol_Update",
            new
            {
                rol.IdRol,
                rol.Nombre,
                rol.Descripcion,
                rol.Activo,
                rol.FechaModificacion,
                rol.UsuarioModificacion
            },
            commandType: CommandType.StoredProcedure);

        return RolMapper.ToDomain(entity);
    }

    public async Task DeleteAsync(int idRol)
    {
        using IDbConnection db = Open();
        await db.ExecuteAsync(
            "seguridad.usp_Rol_Delete",
            new { IdRol = idRol },
            commandType: CommandType.StoredProcedure);
    }
}
