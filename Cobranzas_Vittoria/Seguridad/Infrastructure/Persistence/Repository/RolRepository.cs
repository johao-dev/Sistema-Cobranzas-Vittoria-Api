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

    public async Task<Rol?> GetByIdWithPermisosAsync(int idRol)
    {
        using IDbConnection db = Open();
        const string sql = @"
            SELECT IdRol, Nombre, Descripcion, Activo, FechaCreacion, UsuarioCreacion, FechaModificacion, UsuarioModificacion
            FROM seguridad.Rol WHERE IdRol = @IdRol;

            SELECT p.IdPermiso, p.Codigo, p.Nombre, p.Descripcion, p.Activo, p.FechaCreacion, p.UsuarioCreacion, p.FechaModificacion, p.UsuarioModificacion
            FROM seguridad.Permiso p
            INNER JOIN seguridad.PermisoRol pr ON pr.IdPermiso = p.IdPermiso
            WHERE pr.IdRol = @IdRol;";

        using SqlMapper.GridReader multi = await db.QueryMultipleAsync(sql, new { IdRol = idRol });
        RolEntity? rolEntity = await multi.ReadSingleOrDefaultAsync<RolEntity>();
        if (rolEntity is null)
            return null;

        Rol rol = RolMapper.ToDomain(rolEntity);
        IEnumerable<PermisoEntity> permisos = await multi.ReadAsync<PermisoEntity>();
        rol.EstablecerPermisos(permisos.Select(PermisoMapper.ToDomain));
        return rol;
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

    public async Task AsignarPermisosAsync(int idRol, IEnumerable<int> idPermisos, string usuarioCreacion)
    {
        using IDbConnection db = Open();
        await db.ExecuteAsync(
            "seguridad.usp_Rol_AsignarPermisos",
            new
            {
                IdRol = idRol,
                Permisos = string.Join(',', idPermisos),
                UsuarioCreacion = usuarioCreacion
            },
            commandType: CommandType.StoredProcedure);
    }

    public async Task QuitarPermisoAsync(int idRol, int idPermiso)
    {
        using IDbConnection db = Open();
        await db.ExecuteAsync(
            "seguridad.usp_Rol_QuitarPermiso",
            new { IdRol = idRol, IdPermiso = idPermiso },
            commandType: CommandType.StoredProcedure);
    }
}
