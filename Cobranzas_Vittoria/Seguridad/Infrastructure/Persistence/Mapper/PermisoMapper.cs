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
        return Permiso.Reconstruir(
            entity.IdPermiso,
            entity.Codigo,
            entity.Nombre,
            entity.Descripcion,
            entity.Activo,
            entity.FechaCreacion,
            entity.UsuarioCreacion,
            entity.FechaModificacion,
            entity.UsuarioModificacion);
    }
}