using Cobranzas_Vittoria.Compras.Domain.Excepciones;

namespace Cobranzas_Vittoria.Compras.Domain.Model;

/// <summary>Línea del requerimiento. Su material y su pertenencia se conservan durante las modificaciones.</summary>
public sealed class ItemRequerimiento
{
    public int IdRequerimientoDetalle { get; private set; }
    public int IdRequerimiento { get; private set; }
    public int IdMaterial { get; private set; }
    public int? IdPresupuestoDetalle { get; private set; }
    public decimal Cantidad { get; private set; }
    public string? Observacion { get; private set; }

    private ItemRequerimiento() { }

    public static ItemRequerimiento Crear(
        int idRequerimiento,
        int idMaterial,
        int? idPresupuestoDetalle,
        decimal cantidad,
        string? observacion)
    {
        // Cero representa un agregado nuevo aún sin IDENTITY; nunca es el ID de una fila existente.
        if (idRequerimiento < 0)
            throw ValidacionComprasException.IdentificadorInvalido(nameof(IdRequerimiento));

        return new ItemRequerimiento
        {
            // El detalle recibe su propia identidad al persistir; no se reutiliza la del padre.
            IdRequerimiento = idRequerimiento,
            IdMaterial = ReglasCompras.Id(idMaterial, nameof(IdMaterial)),
            // La imputación es opcional mientras se completa la integración presupuestaria.
            IdPresupuestoDetalle = ReglasCompras.IdOpcional(idPresupuestoDetalle, nameof(IdPresupuestoDetalle)),
            Cantidad = ReglasCompras.Cantidad(cantidad),
            Observacion = ReglasCompras.TextoOpcional(observacion, nameof(Observacion), 250)
        };
    }

    internal ItemRequerimiento Actualizar(
        decimal cantidad,
        int? idPresupuestoDetalle,
        string? observacion)
    {
        // Solo el agregado modifica sus líneas para que no se omita la validación del estado del padre.
        // Se validan todos los campos antes de asignar: un rechazo no deja el objeto a medio actualizar.
        ReglasCompras.Cantidad(cantidad);
        ReglasCompras.IdOpcional(idPresupuestoDetalle, nameof(IdPresupuestoDetalle));
        var texto = ReglasCompras.TextoOpcional(observacion, nameof(Observacion), 250);
        IdPresupuestoDetalle = idPresupuestoDetalle;
        Cantidad = cantidad;
        Observacion = texto;

        return this;
    }

    /// <summary>Restaura datos existentes sin normalizarlos ni ejecutar las reglas de una alta nueva.</summary>
    public static ItemRequerimiento Reconstruir(
        int idRequerimientoDetalle,
        int idRequerimiento,
        int idMaterial,
        int? idPresupuestoDetalle,
        decimal cantidad,
        string? observacion)
    {
        return new ItemRequerimiento
        {
            IdRequerimientoDetalle = idRequerimientoDetalle,
            IdRequerimiento = idRequerimiento,
            IdMaterial = idMaterial,
            IdPresupuestoDetalle = idPresupuestoDetalle,
            Cantidad = cantidad,
            Observacion = observacion
        };
    }
}
