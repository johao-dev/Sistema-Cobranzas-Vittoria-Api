using Cobranzas_Vittoria.Compras.Domain.Excepciones;
using Cobranzas_Vittoria.Compras.Domain.ValueObject;

namespace Cobranzas_Vittoria.Compras.Domain.Model;

public sealed class Requerimiento
{
    public int IdRequerimiento { get; private set; }
    public int IdProyecto { get; private set; }
    public string Estado { get; private set; } = EstadoRequerimiento.Registrado;
    public IEnumerable<ItemRequerimiento> Items => _items;

    private readonly List<ItemRequerimiento> _items = [];

    private Requerimiento() {}

    public static Requerimiento Crear(int idProyecto, List<ItemRequerimiento> items)
    {
        if (items == null || !items.Any())
        {
            throw new ValidacionComprasException("REQUERIMIENTO_SIN_ITEMS",
                "No se puede crear un requerimiento sin items.");
        }

        var requerimiento = new Requerimiento
        {
            IdProyecto = idProyecto
        };

        requerimiento._items.AddRange(items);
        return requerimiento;
    }

    public static Requerimiento Reconstruir(
        int idRequerimiento,
        int idProyecto,
        string estado,
        IEnumerable<ItemRequerimiento> items)
    {
        var requerimiento = new Requerimiento
        {
            IdRequerimiento = idRequerimiento,
            IdProyecto = idProyecto,
            Estado = estado
        };

        requerimiento._items.AddRange(items);
        return requerimiento;
    }

    public ItemRequerimiento AgregarItem(
        int idMaterial,
        int cantidad,
        int? idPresupuestoDetalle,
        string? observacion)
    {
        ValidarGestionItems();
        ValidarMaterialNoDuplicado(idMaterial);

        var item = ItemRequerimiento.Crear(
            IdRequerimiento,
            idMaterial,
            idPresupuestoDetalle,
            cantidad,
            observacion
        );

        _items.Add(item);
        return item;
    }

    private void ValidarMaterialNoDuplicado(int idMaterial)
    {
        if (_items.Any(i => i.IdMaterial == idMaterial))
        {
            throw new ValidacionComprasException("MATERIAL_DUPLICADO",
                "No se puede agregar un material que ya exista en el requerimiento.");
        }
    }

    public ItemRequerimiento ActualizarItem(
        int idDetalle,
        int cantidad,
        int idPresupuestoDetalle,
        string? observacion)
    {
        ValidarGestionItems();

        ItemRequerimiento item = _items.FirstOrDefault(
            i => i.IdRequerimientoDetalle == idDetalle)
            ?? throw new ItemRequerimientoNoEncontradoException(IdRequerimiento, idDetalle);

        item.Actualizar(
            cantidad,
            idPresupuestoDetalle,
            observacion);

        return item;
    }

    public void EliminarItem(int idDetalle)
    {
        ValidarGestionItems();

        var item = _items.FirstOrDefault(i => i.IdRequerimientoDetalle == idDetalle)
            ?? throw new ItemRequerimientoNoEncontradoException(IdRequerimiento, idDetalle);

        _items.Remove(item);
    }

    private void ValidarGestionItems()
    {
        if (Estado != EstadoRequerimiento.EnviadoAlmacen)
        {
            throw new ValidacionComprasException("ESTADO_REQUERIMIENTO_INVALIDO",
                "No se puede gestionar items de un requerimiento que no esté en estado 'EnviadoAlmacen'.");
        }
    }
}
