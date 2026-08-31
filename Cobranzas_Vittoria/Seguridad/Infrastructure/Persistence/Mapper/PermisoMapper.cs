namespace Cobranzas_Vittoria.Seguridad.Infrastructure.Persistence.Mapper;

using Cobranzas_Vittoria.Seguridad.Infrastructure.Persistence.Entity;
using Cobranzas_Vittoria.Seguridad.Domain.Model;

public static class PermisoMapper
{
    public static PermisoEntity ToEntity(Permiso permiso)
    {
        return new PermisoEntity
        {
            IdPermiso = permiso.IdPermiso,
            Codigo = permiso.Codigo,
            Nombre = permiso.Nombre,
            Descripcion = permiso.Descripcion,
            FechaCreacion = permiso.FechaCreacion,
            UsuarioCreacion = permiso.UsuarioCreacion,
            FechaModificacion = permiso.FechaModificacion,
            UsuarioModificacion = permiso.UsuarioModificacion
        };
    }

    public static Permiso ToDomain(PermisoEntity entity)
    {
        return new Permiso
        {
            IdPermiso = entity.IdPermiso,
            Codigo = entity.Codigo,
            Nombre = entity.Nombre,
            Descripcion = entity.Descripcion,
            FechaCreacion = entity.FechaCreacion,
            UsuarioCreacion = entity.UsuarioCreacion,
            FechaModificacion = entity.FechaModificacion,
            UsuarioModificacion = entity.UsuarioModificacion
        };
    }
}