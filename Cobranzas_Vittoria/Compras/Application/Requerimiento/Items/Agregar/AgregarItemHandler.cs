using Cobranzas_Vittoria.Compras.Domain.Excepciones;
using RequerimientoDomain = Cobranzas_Vittoria.Compras.Domain.Model.Requerimiento;
using Cobranzas_Vittoria.Compras.Domain.Model;
using Cobranzas_Vittoria.Compras.Domain.Persistence;

namespace Cobranzas_Vittoria.Compras.Application.Requerimiento.Items.Agregar;

public sealed class AgregarItemHandler(IRequerimientoRepository repository, ILogger<AgregarItemHandler> logger)
{
    public async Task<RequerimientoItemResult> HandleAsync(AgregarItemCommand command, CancellationToken ct = default)
    {
        RequerimientoItemValidator.ValidarIds(command.IdRequerimiento);

        RequerimientoDomain requerimiento = await repository.ObtenerConItemsAsync(command.IdRequerimiento, ct)
            ?? throw new RequerimientoNoEncontradoException(command.IdRequerimiento);

        ItemRequerimiento item = requerimiento.AgregarItem(command.IdMaterial, command.Cantidad,
            command.IdPresupuestoDetalle, command.Observacion);

        ItemRequerimiento creado = await repository.AgregarItemAsync(command.IdRequerimiento, item, ct);

        logger.LogInformation("Ítem {Detalle} agregado al requerimiento {Requerimiento}",
            creado.IdRequerimientoDetalle, command.IdRequerimiento);
        return RequerimientoItemResult.Desde(creado);
    }
}
