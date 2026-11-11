using Cobranzas_Vittoria.Application.Common.Eventos;

namespace Cobranzas_Vittoria.Infrastructure.Eventos;

/// <summary>
/// Usa los servicios del scope emisor en orden de registro. El primer fallo se propaga
/// y los suscriptores restantes no se ejecutan. No almacena ni reintenta eventos.
/// </summary>
public sealed class InMemoryEventPublisher(IServiceProvider services) : IEventPublisher
{
    public async Task PublishAsync<TEvent>(TEvent evento, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evento);
        cancellationToken.ThrowIfCancellationRequested();

        IEnumerable<IEventHandler<TEvent>> handlers = services.GetServices<IEventHandler<TEvent>>();

        foreach (IEventHandler<TEvent> handler in handlers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await handler.HandleAsync(evento, cancellationToken);
        }
    }
}
