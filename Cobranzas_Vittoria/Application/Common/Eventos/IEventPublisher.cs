namespace Cobranzas_Vittoria.Application.Common.Eventos;

/// <summary>Distribuye una ocurrencia dentro del proceso y espera a sus suscriptores.</summary>
public interface IEventPublisher
{
    Task PublishAsync<TEvent>(TEvent evento, CancellationToken cancellationToken = default);
}
