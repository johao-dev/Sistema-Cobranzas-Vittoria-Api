using Cobranzas_Vittoria.Compras.Domain.Excepciones;
using Cobranzas_Vittoria.Compras.Domain.ValueObject;

namespace Cobranzas_Vittoria.Compras.Domain.Model;

/// <summary>
/// Agregado de cabecera e ítems. Los nombres de proyecto, solicitante y material son datos de consulta,
/// no estado propio del requerimiento. La existencia de referencias y la compatibilidad presupuestaria
/// se comprueban en persistencia; este modelo no consulta otros módulos.
/// </summary>
public sealed class Requerimiento
{
    public int IdRequerimiento { get; private set; }
    public string NumeroRequerimiento { get; private set; } = string.Empty;
    public DateOnly FechaRequerimiento { get; private set; }
    public int IdProyecto { get; private set; }
    public string? Descripcion { get; private set; }
    public DateOnly? FechaEntrega { get; private set; }
    public int IdUsuarioSolicitante { get; private set; }
    public string Estado { get; private set; } = EstadoRequerimiento.Registrado;
    public string? Observacion { get; private set; }
    public DateTime FechaCreacion { get; private set; }
    public IReadOnlyList<ItemRequerimiento> Items { get; }

    private readonly List<ItemRequerimiento> _items = [];

    private Requerimiento()
    {
        // La vista impide agregar o quitar elementos saltándose las reglas del agregado.
        Items = _items.AsReadOnly();
    }

    public static Requerimiento Crear(
        string numeroRequerimiento,
        DateOnly fechaRequerimiento,
        int idProyecto,
        int idUsuarioSolicitante,
        IEnumerable<ItemRequerimiento> items,
        string? descripcion = null,
        DateOnly? fechaEntrega = null,
        string? observacion = null,
        DateTime? fechaCreacion = null)
    {
        Requerimiento requerimiento = new Requerimiento
        {
            // La numeración se obtiene fuera del dominio: asegurar unicidad exige acceder a la BD.
            NumeroRequerimiento = ReglasCompras.TextoRequerido(numeroRequerimiento, nameof(NumeroRequerimiento), 30),
            IdProyecto = ReglasCompras.Id(idProyecto, nameof(IdProyecto)),
            IdUsuarioSolicitante = ReglasCompras.Id(idUsuarioSolicitante, nameof(IdUsuarioSolicitante)),
            // Los límites corresponden a las columnas actuales; evitan truncamiento al persistir.
            Descripcion = ReglasCompras.TextoOpcional(descripcion, nameof(Descripcion), 250),
            Observacion = ReglasCompras.TextoOpcional(observacion, nameof(Observacion), 250),
            FechaRequerimiento = fechaRequerimiento,
            FechaEntrega = fechaEntrega,
            // Se permite suministrar el instante desde el caso de uso para usar un reloj controlado.
            FechaCreacion = fechaCreacion ?? DateTime.UtcNow
        };

        ValidarFechas(fechaRequerimiento, fechaEntrega);
        ArgumentNullException.ThrowIfNull(items);

        foreach (ItemRequerimiento item in items)
        {
            ArgumentNullException.ThrowIfNull(item);

            // Un alta no debe apropiarse de líneas persistidas de otro documento.
            if (item.IdRequerimiento != 0 || item.IdRequerimientoDetalle != 0)
                throw new ConflictoComprasException("REQUERIMIENTO_ITEM_YA_ASOCIADO",
                    "Un requerimiento nuevo solo admite ítems nuevos sin identidad persistida.");

            requerimiento._items.Add(CopiarItem(item));
        }

        requerimiento.ValidarItemsParaEnvio();

        return requerimiento;
    }

    /// <summary>Reconstruye el agregado completo y conserva exactamente la cabecera y sus líneas.</summary>
    public static Requerimiento Reconstruir(
        int idRequerimiento,
        string numeroRequerimiento,
        DateOnly fechaRequerimiento,
        int idProyecto,
        string? descripcion,
        DateOnly? fechaEntrega,
        int idUsuarioSolicitante,
        string estado,
        string? observacion,
        DateTime fechaCreacion,
        IEnumerable<ItemRequerimiento> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        Requerimiento requerimiento = new Requerimiento
        {
            IdRequerimiento = ReglasCompras.Id(idRequerimiento, nameof(IdRequerimiento)),
            NumeroRequerimiento = numeroRequerimiento,
            FechaRequerimiento = fechaRequerimiento,
            IdProyecto = idProyecto,
            Descripcion = descripcion,
            FechaEntrega = fechaEntrega,
            IdUsuarioSolicitante = idUsuarioSolicitante,
            Estado = estado,
            Observacion = observacion,
            FechaCreacion = fechaCreacion
        };

        // La lectura no es un alta: se conserva incluso un documento histórico vacío o con valores
        // que hoy no aceptaríamos. Las reglas de escritura se aplican al ejecutar cada operación.
        HashSet<int> ids = new HashSet<int>();

        foreach (ItemRequerimiento item in items)
        {
            ArgumentNullException.ThrowIfNull(item);

            // Sí se protege la estructura del agregado: mezclar padres o repetir una identidad indica
            // una consulta/mapeo incorrecto, no un dato que deba corregirse silenciosamente.
            if (item.IdRequerimiento != idRequerimiento)
                throw new ConflictoComprasException("REQUERIMIENTO_ITEM_NO_PERTENECE",
                    "Todos los ítems reconstruidos deben pertenecer al requerimiento indicado.");

            ReglasCompras.Id(item.IdRequerimientoDetalle, nameof(ItemRequerimiento.IdRequerimientoDetalle));

            if (!ids.Add(item.IdRequerimientoDetalle))
                throw new ConflictoComprasException("REQUERIMIENTO_DETALLE_DUPLICADO",
                    "La reconstrucción contiene más de una fila para el mismo detalle.");

            requerimiento._items.Add(CopiarItem(item));
        }

        return requerimiento;
    }

    public void ActualizarDatos(
        int idUsuarioActual,
        string numeroRequerimiento,
        DateOnly fechaRequerimiento,
        string? descripcion,
        DateOnly? fechaEntrega,
        string? observacion)
    {
        ValidarEstado(EstadoRequerimiento.Registrado);
        ValidarSolicitante(idUsuarioActual);

        string numero = ReglasCompras.TextoRequerido(numeroRequerimiento, nameof(NumeroRequerimiento), 30);
        string? textoDescripcion = ReglasCompras.TextoOpcional(descripcion, nameof(Descripcion), 250);
        string? textoObservacion = ReglasCompras.TextoOpcional(observacion, nameof(Observacion), 250);

        ValidarFechas(fechaRequerimiento, fechaEntrega);

        // Proyecto y solicitante identifican el contexto del documento. Cambiar el proyecto necesita
        // una operación explícita que revalide las partidas, no una edición genérica de cabecera.
        NumeroRequerimiento = numero;
        FechaRequerimiento = fechaRequerimiento;
        Descripcion = textoDescripcion;
        FechaEntrega = fechaEntrega;
        Observacion = textoObservacion;
    }

    public ItemRequerimiento AgregarItem(
        int idMaterial,
        decimal cantidad,
        int? idPresupuestoDetalle,
        string? observacion)
    {
        ValidarEstado(EstadoRequerimiento.EnviadoAlmacen);

        // Un material representa una sola necesidad dentro del documento; modificar su cantidad
        // evita líneas duplicadas que luego harían ambigua la generación de la orden de compra.
        if (_items.Any(i => i.IdMaterial == idMaterial))
            throw new ConflictoComprasException("MATERIAL_DUPLICADO",
                "No se puede agregar un material que ya exista en el requerimiento.");

        ItemRequerimiento item = ItemRequerimiento.Crear(IdRequerimiento, idMaterial, idPresupuestoDetalle, cantidad, observacion);
        _items.Add(item);

        return item;
    }

    public ItemRequerimiento ActualizarItem(
        int idDetalle,
        decimal cantidad,
        int? idPresupuestoDetalle,
        string? observacion)
    {
        ValidarEstado(EstadoRequerimiento.EnviadoAlmacen);

        // El material no cambia: sustituirlo implica agregar la nueva necesidad y quitar la anterior.
        return ObtenerItem(idDetalle).Actualizar(cantidad, idPresupuestoDetalle, observacion);
    }

    public void EliminarItem(int idDetalle)
    {
        ValidarEstado(EstadoRequerimiento.EnviadoAlmacen);

        ItemRequerimiento item = ObtenerItem(idDetalle);

        // Un requerimiento vacío ya no expresa una solicitud. Retirar el documento es otro flujo.
        if (_items.Count == 1)
            throw new ConflictoComprasException("REQUERIMIENTO_SIN_ITEMS",
                "No se puede eliminar el último ítem del requerimiento.");

        _items.Remove(item);
    }

    public void Enviar(int idUsuarioActual, string? observacion = null)
    {
        ValidarEstado(EstadoRequerimiento.Registrado);
        ValidarSolicitante(idUsuarioActual);

        // La solicitud debe ser utilizable al salir de borrador, incluso si provino de datos legacy.
        ValidarItemsParaEnvio();

        CambiarEstado(EstadoRequerimiento.EnviadoAlmacen, observacion);
    }

    public void ProcesarStock(string resultado, string? observacion = null)
    {
        ValidarEstado(EstadoRequerimiento.EnviadoAlmacen);

        // Se conservan las dos salidas del SP actual: conforme continúa, observado vuelve al solicitante.
        string destino = resultado switch
        {
            "Conforme" => EstadoRequerimiento.ValidadoAlmacen,
            "Observado" => EstadoRequerimiento.Registrado,
            _ => throw new ValidacionComprasException("REQUERIMIENTO_RESULTADO_INVALIDO",
                "El resultado de almacén debe ser Conforme u Observado.")
        };

        if (resultado == "Conforme")
            ValidarItemsParaEnvio();

        // El caso de uso debe persistir también RequerimientoValidacion en la misma transacción.
        CambiarEstado(destino, observacion);
    }

    public void Aprobar(string? observacion = null)
    {
        // La aprobación corresponde a una solicitud ya revisada por almacén, como exige el flujo actual.
        ValidarEstado(EstadoRequerimiento.ValidadoAlmacen);

        CambiarEstado(EstadoRequerimiento.AprobadoCoordinador, observacion);
    }

    public void Rechazar(string? observacion = null)
    {
        // Coordinación puede rechazar antes o después de aprobar, pero nunca después de enviar a OC.
        ValidarEstado(EstadoRequerimiento.ValidadoAlmacen, EstadoRequerimiento.AprobadoCoordinador);

        CambiarEstado(EstadoRequerimiento.Rechazado, observacion);
    }

    public void EnviarCompras(string? observacion = null)
    {
        ValidarEstado(EstadoRequerimiento.AprobadoCoordinador);

        CambiarEstado(EstadoRequerimiento.EnviadoOC, observacion);
    }

    public void MarcarOrdenCompraGenerada()
    {
        // Este cambio se persiste junto a la creación de la OC; el modelo no puede garantizar por sí
        // solo que no exista ya una orden. Esa unicidad se protege dentro de la transacción de BD.
        ValidarEstado(EstadoRequerimiento.EnviadoOC);

        Estado = EstadoRequerimiento.GeneradoOC;
    }

    private ItemRequerimiento ObtenerItem(int idDetalle)
    {
        ReglasCompras.Id(idDetalle, nameof(ItemRequerimiento.IdRequerimientoDetalle));

        return _items.FirstOrDefault(i => i.IdRequerimientoDetalle == idDetalle)
            ?? throw new ItemRequerimientoNoEncontradoException(IdRequerimiento, idDetalle);
    }

    private void ValidarEstado(params string[] permitidos)
    {
        // Los permisos/roles se resuelven en aplicación; el estado permitido es una regla del documento.
        if (!permitidos.Contains(Estado, StringComparer.Ordinal))
            throw new ConflictoComprasException("ESTADO_REQUERIMIENTO_INVALIDO",
                $"La operación requiere estado {string.Join(" o ", permitidos)}; el estado actual es {Estado}.");
    }

    private void ValidarSolicitante(int idUsuarioActual)
    {
        ReglasCompras.Id(idUsuarioActual, nameof(idUsuarioActual));

        // La identidad del solicitante sí pertenece al documento y protege edición/envío del borrador.
        if (idUsuarioActual != IdUsuarioSolicitante)
            throw new ConflictoComprasException("REQUERIMIENTO_SOLICITANTE_REQUERIDO",
                "Solo el usuario solicitante puede editar o enviar el requerimiento registrado.");
    }

    private void ValidarItemsParaEnvio()
    {
        if (_items.Count == 0)
            throw new ValidacionComprasException("REQUERIMIENTO_SIN_ITEMS",
                "El requerimiento debe contener al menos un ítem.");

        if (_items.GroupBy(i => i.IdMaterial).Any(grupo => grupo.Count() > 1))
            throw new ConflictoComprasException("MATERIAL_DUPLICADO",
                "Cada material puede aparecer una sola vez dentro del requerimiento.");

        foreach (ItemRequerimiento item in _items)
        {
            ReglasCompras.Id(item.IdMaterial, nameof(ItemRequerimiento.IdMaterial));
            ReglasCompras.IdOpcional(item.IdPresupuestoDetalle, nameof(ItemRequerimiento.IdPresupuestoDetalle));
            ReglasCompras.Cantidad(item.Cantidad);
            ReglasCompras.TextoOpcional(item.Observacion, nameof(ItemRequerimiento.Observacion), 250);
        }
    }

    private static void ValidarFechas(DateOnly fechaRequerimiento, DateOnly? fechaEntrega)
    {
        // No se admiten fechas omitidas ni entregas anteriores a la solicitud. No se compara con hoy:
        // el sistema necesita permitir el registro de documentos atrasados o programados.
        if (fechaRequerimiento == default)
            throw ValidacionComprasException.CampoRequerido(nameof(FechaRequerimiento));

        if (fechaEntrega < fechaRequerimiento)
            throw new ValidacionComprasException("REQUERIMIENTO_FECHAS_INVALIDAS",
                "La fecha de entrega no puede ser anterior a la fecha del requerimiento.");
    }

    private void CambiarEstado(string destino, string? observacion)
    {
        // En las transiciones, null conserva la observación previa, igual que COALESCE en los SP.
        string? texto = observacion is null ? Observacion
            : ReglasCompras.TextoOpcional(observacion, nameof(Observacion), 250);

        Estado = destino;
        Observacion = texto;
    }

    private static ItemRequerimiento CopiarItem(ItemRequerimiento item)
        // El agregado conserva sus propias instancias; modificar otro agregado no debe alterar este.
        => ItemRequerimiento.Reconstruir(item.IdRequerimientoDetalle, item.IdRequerimiento, item.IdMaterial,
            item.IdPresupuestoDetalle, item.Cantidad, item.Observacion);
}
