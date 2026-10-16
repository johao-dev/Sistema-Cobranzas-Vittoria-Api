using Cobranzas_Vittoria.Compras.Domain.Excepciones;
using RequerimientoDomain = Cobranzas_Vittoria.Compras.Domain.Model.Requerimiento;
using Cobranzas_Vittoria.Compras.Domain.Persistence;

namespace Cobranzas_Vittoria.Compras.Application.Requerimiento.Items.Eliminar;

public sealed class EliminarItemHandler(IRequerimientoRepository repository, ILogger<EliminarItemHandler> logger)
{
    public async Task HandleAsync(EliminarItemCommand command, CancellationToken ct = default)
    {
        RequerimientoItemValidator.ValidarIds(command.IdRequerimiento, command.IdDetalle);

        RequerimientoDomain requerimiento = await repository.ObtenerConItemsAsync(command.IdRequerimiento, ct)
            ?? throw new RequerimientoNoEncontradoException(command.IdRequerimiento);

        requerimiento.EliminarItem(command.IdDetalle);

        await repository.EliminarItemAsync(command.IdRequerimiento, command.IdDetalle, ct);

        logger.LogInformation("Ítem {Detalle} eliminado del requerimiento {Requerimiento}",
            command.IdDetalle, command.IdRequerimiento);
    }
}
