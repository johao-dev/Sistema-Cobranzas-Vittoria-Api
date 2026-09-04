using Cobranzas_Vittoria.Seguridad.Domain.Model;
using Cobranzas_Vittoria.Seguridad.Infrastructure.Persistence.Entity;

namespace Cobranzas_Vittoria.Seguridad.Infrastructure.Persistence.Mapper;

public static class UsuarioMapper
{
    public static UsuarioEntity ToEntity(Usuario usuario)
    {
        return new UsuarioEntity
        {
            IdUsuario = usuario.IdUsuario,
            Nombres = usuario.Nombres,
            Apellidos = usuario.Apellidos,
            Correo = usuario.Correo.Value,
            UsuarioLogin = usuario.UsuarioLogin,
            PasswordHash = usuario.PasswordHash,
            Activo = usuario.Activo,
            FechaCreacion = usuario.FechaCreacion,
            UsuarioCreacion = usuario.UsuarioCreacion
        };
    }

    public static Usuario ToDomain(UsuarioEntity entity)
    {
        return Usuario.Reconstruir(
            entity.IdUsuario,
            entity.Nombres,
            entity.Apellidos,
            entity.Correo,
            entity.UsuarioLogin,
            entity.PasswordHash,
            entity.Activo,
            entity.UsuarioCreacion ?? string.Empty,
            entity.FechaCreacion ?? DateTime.MinValue);
    }
}
