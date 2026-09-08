using System.Data;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Interfaces;
using Cobranzas_Vittoria.Seguridad.Domain.Model;
using Cobranzas_Vittoria.Seguridad.Infrastructure.Persistence.Entity;
using Cobranzas_Vittoria.Seguridad.Infrastructure.Persistence.Mapper;
using Dapper;

namespace Cobranzas_Vittoria.Repositories;

public class UsuarioRepository : RepositoryBase, IUsuarioRepository
{
    public UsuarioRepository(IDbConnectionFactory factory) : base(factory) { }

    public async Task<Usuario?> GetByIdAsync(int idUsuario)
    {
        using IDbConnection db = Open();
        UsuarioEntity? usuarioEntity = await db.QueryFirstOrDefaultAsync<UsuarioEntity>(
            "seguridad.usp_Usuario_GetById",
            new { UsuarioId = idUsuario },
            commandType: CommandType.StoredProcedure);

        return usuarioEntity is null ? null : UsuarioMapper.ToDomain(usuarioEntity);
    }

    public async Task<Usuario?> GetByIdWithRolesAsync(int idUsuario)
    {
        using IDbConnection db = Open();

        using SqlMapper.GridReader multi = await db.QueryMultipleAsync(
            "seguridad.usp_Usuario_GetByIdWithRoles",
            new { UsuarioId = idUsuario },
            commandType: CommandType.StoredProcedure);

        UsuarioEntity? usuarioEntity = await multi.ReadSingleOrDefaultAsync<UsuarioEntity>();

        if (usuarioEntity is null)
            return null;

        Usuario usuario = UsuarioMapper.ToDomain(usuarioEntity);
        IEnumerable<RolEntity> roles = await multi.ReadAsync<RolEntity>();
        usuario.AsignarRoles(roles.Select(RolMapper.ToDomain));
        return usuario;
    }

    public async Task<Usuario?> GetByCorreoAsync(string correo)
    {
        using IDbConnection db = Open();
        UsuarioEntity? usuarioEntity = await db.QueryFirstOrDefaultAsync<UsuarioEntity>(
            "seguridad.usp_Usuario_GetByCorreo",
            new { Correo = correo },
            commandType: CommandType.StoredProcedure);

        return usuarioEntity is null ? null : UsuarioMapper.ToDomain(usuarioEntity);
    }

    public async Task<Usuario?> GetByLoginAsync(string usuarioLogin)
    {
        using IDbConnection db = Open();
        UsuarioEntity? usuarioEntity = await db.QueryFirstOrDefaultAsync<UsuarioEntity>(
            "seguridad.usp_Usuario_GetByLogin",
            new { UsuarioLogin = usuarioLogin },
            commandType: CommandType.StoredProcedure);

        return usuarioEntity is null ? null : UsuarioMapper.ToDomain(usuarioEntity);
    }

    public async Task<IEnumerable<Usuario>> GetAllAsync(bool? activo = true)
    {
        using IDbConnection db = Open();
        IEnumerable<UsuarioEntity> usuariosEntities = await db.QueryAsync<UsuarioEntity>(
            "seguridad.usp_Usuario_List",
            new { Activo = activo },
            commandType: CommandType.StoredProcedure);

        return usuariosEntities.Select(UsuarioMapper.ToDomain);
    }

    public async Task<Usuario> AddAsync(Usuario usuario)
    {
        using IDbConnection db = Open();
        UsuarioEntity entity = await db.QueryFirstAsync<UsuarioEntity>(
            "seguridad.usp_Usuario_Insert",
            new
            {
                usuario.Nombres,
                usuario.Apellidos,
                Correo = usuario.Correo.Value,
                usuario.UsuarioLogin,
                usuario.PasswordHash,
                usuario.Activo,
                usuario.UsuarioCreacion
            },
            commandType: CommandType.StoredProcedure);

        return UsuarioMapper.ToDomain(entity);
    }

    public async Task<Usuario> UpdateAsync(Usuario usuario)
    {
        using IDbConnection db = Open();
        await db.ExecuteAsync(
            "seguridad.usp_Usuario_Update",
            new
            {
                usuario.IdUsuario,
                usuario.Nombres,
                usuario.Apellidos,
                Correo = usuario.Correo.Value,
                usuario.UsuarioLogin,
                usuario.PasswordHash,
                usuario.Activo
            },
            commandType: CommandType.StoredProcedure);

        return usuario;
    }

    public async Task AsignarRolesAsync(int idUsuario, IEnumerable<int> idRoles)
    {
        using IDbConnection db = Open();
        await db.ExecuteAsync(
            "seguridad.usp_Usuario_AsignarRoles",
            new
            {
                UsuarioId = idUsuario,
                Roles = string.Join(",", idRoles)
            },
            commandType: CommandType.StoredProcedure);
    }

    public async Task QuitarRolAsync(int idUsuario, int idRol)
    {
        using IDbConnection db = Open();
        await db.ExecuteAsync(
            "seguridad.usp_Usuario_QuitarRol",
            new
            {
                UsuarioId = idUsuario,
                RolId = idRol
            },
            commandType: CommandType.StoredProcedure);
    }
}
