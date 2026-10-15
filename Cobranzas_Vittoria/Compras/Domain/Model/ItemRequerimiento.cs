using Cobranzas_Vittoria.Compras.Domain.Excepciones;

namespace Cobranzas_Vittoria.Compras.Domain.Model;

public sealed class ItemRequerimiento
{
    public int IdRequerimientoDetalle { get; private set;}
    public int IdRequerimiento { get; private set; }
    public int IdMaterial { get; private set; }
    public int? IdPresupuestoDetalle { get; private set; }
    public int Cantidad { get; private set; }
    public string? Observacion { get; private set; } = string.Empty;

    private ItemRequerimiento() {}

    public static ItemRequerimiento Crear(
        int idRequerimiento,
        int idMaterial,
        int? idPresupuestoDetalle,
        int cantidad,
        string? observacion)
    {
        ValidarPositivos(idRequerimiento, idMaterial, idPresupuestoDetalle ?? 0, cantidad);
        ValidarObservacion(observacion);
        ItemRequerimiento detalle = new()
        {
            IdRequerimientoDetalle = idRequerimiento,
            IdRequerimiento = idRequerimiento,
            IdMaterial = idMaterial,
            IdPresupuestoDetalle = idPresupuestoDetalle,
            Cantidad = cantidad,
            Observacion = observacion
        };

        return detalle;
    }

    public ItemRequerimiento Actualizar(
        int cantidad,
        int? idPresupuestoDetalle,
        string? observacion)
    {
        ValidarPositivos(idPresupuestoDetalle ?? 0, cantidad);
        ValidarObservacion(observacion);

        IdPresupuestoDetalle = idPresupuestoDetalle;
        Cantidad = cantidad;
        Observacion = observacion;

        return this;
    }

    private static void ValidarPositivos(params int[] valores)
    {
        foreach (var valor in valores)
        {
            if (valor <= 0)
            {
                throw ValidacionComprasException
                    .CampoRequerido("Los identificadores y la cantidad deben ser positivos.");
            }
        }
    }

    private static void ValidarObservacion(string? observacion)
    {
        if (observacion is not null && observacion.Length > 250)
        {
            throw ValidacionComprasException.Longitud("Observación", 250);
        }
    }

    public static ItemRequerimiento Reconstruir(
        int idRequerimientoDetalle,
        int idRequerimiento,
        int idMaterial,
        int idPresupuestoDetalle,
        int cantidad,
        string? observacion)
    {
        ItemRequerimiento detalle = new()
        {
            IdRequerimientoDetalle = idRequerimientoDetalle,
            IdRequerimiento = idRequerimiento,
            IdMaterial = idMaterial,
            IdPresupuestoDetalle = idPresupuestoDetalle,
            Cantidad = cantidad,
            Observacion = observacion
        };

        return detalle;
    }
}
