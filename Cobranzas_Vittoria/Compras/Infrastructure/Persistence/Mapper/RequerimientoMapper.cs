using Cobranzas_Vittoria.Compras.Domain.Model;
using Cobranzas_Vittoria.Compras.Infrastructure.Persistence.Entity;

namespace Cobranzas_Vittoria.Compras.Infrastructure.Persistence.Mapper;

/// <summary>Transforma cabecera y detalles sin ejecutar altas, transiciones ni normalizar datos persistidos.</summary>
public static class RequerimientoMapper
{
    public static RequerimientoEntity ToEntity(Requerimiento domain)
    {
        ArgumentNullException.ThrowIfNull(domain);
        return new RequerimientoEntity
        {
            IdRequerimiento = domain.IdRequerimiento,
            NumeroRequerimiento = domain.NumeroRequerimiento,
            FechaRequerimiento = domain.FechaRequerimiento,
            IdProyecto = domain.IdProyecto,
            Descripcion = domain.Descripcion,
            FechaEntrega = domain.FechaEntrega,
            IdUsuarioSolicitante = domain.IdUsuarioSolicitante,
            Estado = domain.Estado,
            Observacion = domain.Observacion,
            FechaCreacion = domain.FechaCreacion
        };
    }

    public static Requerimiento ToDomain(RequerimientoEntity entity, IEnumerable<RequerimientoDetalleEntity> items)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentNullException.ThrowIfNull(items);
        // Se exigen los detalles explícitamente: una cabecera sin consultar líneas no es un agregado
        // completo y podría permitir duplicados o impedir eliminaciones por contar ítems incorrectamente.
        return Requerimiento.Reconstruir(entity.IdRequerimiento, entity.NumeroRequerimiento,
            entity.FechaRequerimiento, entity.IdProyecto, entity.Descripcion, entity.FechaEntrega,
            entity.IdUsuarioSolicitante, entity.Estado, entity.Observacion, entity.FechaCreacion,
            items.Select(ToDomain));
    }

    public static RequerimientoDetalleEntity ToEntity(ItemRequerimiento domain)
    {
        ArgumentNullException.ThrowIfNull(domain);
        return new RequerimientoDetalleEntity
        {
            IdRequerimientoDetalle = domain.IdRequerimientoDetalle,
            IdRequerimiento = domain.IdRequerimiento,
            IdMaterial = domain.IdMaterial,
            IdPresupuestoDetalle = domain.IdPresupuestoDetalle,
            Cantidad = domain.Cantidad,
            Observacion = domain.Observacion
        };
    }

    public static ItemRequerimiento ToDomain(RequerimientoDetalleEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return ItemRequerimiento.Reconstruir(entity.IdRequerimientoDetalle, entity.IdRequerimiento,
            entity.IdMaterial, entity.IdPresupuestoDetalle, entity.Cantidad, entity.Observacion);
    }

    /// <summary>Devuelve las filas separadas porque la cabecera y los detalles pertenecen a tablas distintas.</summary>
    public static (RequerimientoEntity Requerimiento, IReadOnlyList<RequerimientoDetalleEntity> Items)
        ToEntityConItems(Requerimiento domain)
    {
        ArgumentNullException.ThrowIfNull(domain);
        return (ToEntity(domain), domain.Items.Select(ToEntity).ToList());
    }
}
