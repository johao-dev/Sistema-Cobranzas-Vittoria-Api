using Cobranzas_Vittoria.Seguridad.Domain.Model;
using Cobranzas_Vittoria.Seguridad.Infrastructure.Persistence.Entity;

namespace Cobranzas_Vittoria.Seguridad.Infrastructure.Persistence.Mapper;

public static class RolMapper
{
    public static RolEntity ToEntity(Rol rol)
    {
        return new RolEntity
        {
            IdRol = rol.IdRol,
            Nombre = rol.Nombre,
            Descripcion = rol.Descripcion,
            Activo = rol.Activo,
            FechaCreacion = rol.FechaCreacion,
            UsuarioCreacion = rol.UsuarioCreacion,
            FechaModificacion = rol.FechaModificacion,
            UsuarioModificacion = rol.UsuarioModificacion
        };
    }
    
    public static Rol ToDomain(RolEntity entity)
    {
        return Rol.Reconstruir(
            entity.IdRol,
            entity.Nombre,
            entity.Descripcion,
            entity.Activo,
            entity.FechaCreacion,
            entity.UsuarioCreacion,
            entity.FechaModificacion,
            entity.UsuarioModificacion
        );
    }

    // TODO: Quiza se necesite reconstruir los permisos tambien
}