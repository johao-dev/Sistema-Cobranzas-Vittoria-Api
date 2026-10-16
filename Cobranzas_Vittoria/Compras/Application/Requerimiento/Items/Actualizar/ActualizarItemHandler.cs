using Cobranzas_Vittoria.Compras.Domain.Excepciones;
using RequerimientoDomain = Cobranzas_Vittoria.Compras.Domain.Model.Requerimiento;
using Cobranzas_Vittoria.Compras.Domain.Persistence;
using Cobranzas_Vittoria.Compras.Domain.ValueObject;
using Cobranzas_Vittoria.Compras.Domain.Model;

namespace Cobranzas_Vittoria.Compras.Application.Requerimiento.Items.Actualizar;

public sealed class ActualizarItemHandler(IRequerimientoRepository repository, ILogger<ActualizarItemHandler> logger)
{
    public async Task<RequerimientoItemResult> HandleAsync(ActualizarItemCommand command, CancellationToken ct = default)
    {
        RequerimientoItemValidator.ValidarIds(command.IdRequerimiento, command.IdDetalle);
        RequerimientoItemValidator.ValidarCampos(command.Campos, command.Cantidad);

        RequerimientoDomain requerimiento = await repository.ObtenerConItemsAsync(command.IdRequerimiento, ct)
            ?? throw new RequerimientoNoEncontradoException(command.IdRequerimiento);

        ItemRequerimiento actual = requerimiento.Items.FirstOrDefault(i => i.IdRequerimientoDetalle == command.IdDetalle)
            ?? throw new ItemRequerimientoNoEncontradoException(command.IdRequerimiento, command.IdDetalle);

        // La presencia, no el valor, distingue conservar de limpiar los campos opcionales.
        ItemRequerimiento item = requerimiento.ActualizarItem(command.IdDetalle,
            command.Campos.HasFlag(CamposItemRequerimiento.Cantidad) ? command.Cantidad!.Value : actual.Cantidad,
            command.Campos.HasFlag(CamposItemRequerimiento.Partida) ? command.IdPresupuestoDetalle : actual.IdPresupuestoDetalle,
            command.Campos.HasFlag(CamposItemRequerimiento.Observacion) ? command.Observacion : actual.Observacion);

        ItemRequerimiento actualizado = await repository.ActualizarItemAsync(command.IdRequerimiento, item, command.Campos, ct);
        
        logger.LogInformation("Ítem {Detalle} actualizado en el requerimiento {Requerimiento}",
            command.IdDetalle, command.IdRequerimiento);
        return RequerimientoItemResult.Desde(actualizado);
    }
}
