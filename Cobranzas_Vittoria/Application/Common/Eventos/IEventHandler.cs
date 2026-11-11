namespace Cobranzas_Vittoria.Application.Common.Eventos;

public interface IEventHandler<in TEvent>
{
    Task HandleAsync(TEvent evento, CancellationToken cancellationToken = default);
}
