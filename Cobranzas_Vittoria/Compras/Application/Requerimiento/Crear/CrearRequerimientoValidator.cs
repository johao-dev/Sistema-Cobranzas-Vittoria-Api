using Cobranzas_Vittoria.Compras.Domain.Excepciones;
using Cobranzas_Vittoria.Compras.Domain.Model;

namespace Cobranzas_Vittoria.Compras.Application.Requerimiento.Crear;

public static class CrearRequerimientoValidator
{
    public static IReadOnlyList<ItemRequerimiento> ValidarEntrada(CrearRequerimientoCommand command)
    {
        ReglasCompras.Id(command.IdProyecto, nameof(command.IdProyecto));
        ReglasCompras.TextoOpcionalSinNormalizar(command.NumeroRequerimiento?.Trim(), nameof(command.NumeroRequerimiento), 30);
        ReglasCompras.TextoOpcionalSinNormalizar(command.Descripcion, nameof(command.Descripcion), 250);
        ReglasCompras.TextoOpcionalSinNormalizar(command.Observacion, nameof(command.Observacion), 250);

        if (command.Items is null || command.Items.Count == 0)
            throw new ValidacionComprasException("REQUERIMIENTO_SIN_ITEMS", "El requerimiento debe contener al menos un ítem.");

        List<ItemRequerimiento> items = new List<ItemRequerimiento>();
        HashSet<int> materiales = new HashSet<int>();

        foreach (CrearItemRequerimientoCommand? entrada in command.Items)
        {
            if (entrada is null)
                throw ValidacionComprasException.CampoRequerido("items");

            ItemRequerimiento item = ItemRequerimiento.CrearParaNuevoRequerimiento(entrada.IdMaterial,
                entrada.IdPresupuestoDetalle, entrada.Cantidad, entrada.Observacion);

            if (!materiales.Add(item.IdMaterial))
                throw new ConflictoComprasException("MATERIAL_DUPLICADO", "Cada material puede aparecer una sola vez dentro del requerimiento.");

            items.Add(item);
        }

        return items;
    }

    public static void ValidarContexto(ContextoCreacionRequerimiento contexto, int idProyecto,
        IReadOnlyList<ItemRequerimiento> items)
    {
        if (!contexto.ExisteProyecto)
            throw new RecursoComprasNoEncontradoException("PROYECTO_NO_EXISTE", "El proyecto no existe.");

        if (!contexto.ExisteSolicitante)
            throw new RecursoComprasNoEncontradoException("SOLICITANTE_NO_EXISTE", "El usuario solicitante no existe.");

        Dictionary<int, PartidaCreacionRequerimiento> partidas = contexto.Partidas.ToDictionary(p => p.IdPresupuestoDetalle);

        foreach (ItemRequerimiento item in items)
        {
            if (!contexto.MaterialesExistentes.Contains(item.IdMaterial))
                throw new RecursoComprasNoEncontradoException("MATERIAL_NO_EXISTE", "Uno de los materiales no existe.");

            if (item.IdPresupuestoDetalle is not int idPartida)
                continue;

            if (!partidas.TryGetValue(idPartida, out PartidaCreacionRequerimiento? partida)
                || !string.Equals(partida.EstadoPresupuesto, "APROBADO", StringComparison.OrdinalIgnoreCase)
                || !partida.PresupuestoActivo || !partida.CentroCostoActivo || partida.IdProyecto != idProyecto)
            {
                throw new ConflictoComprasException("PARTIDA_REQUERIMIENTO_INVALIDA",
                    "La partida debe pertenecer a una versión APROBADA con presupuesto y centro de costo activos del proyecto.");
            }
        }
    }
}
