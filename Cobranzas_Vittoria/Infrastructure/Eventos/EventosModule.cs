using Cobranzas_Vittoria.Application.Common.Eventos;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cobranzas_Vittoria.Infrastructure.Eventos;

public static class EventosModule
{
    public static IServiceCollection AddEventosEnMemoria(this IServiceCollection services)
    {
        services.TryAddScoped<IEventPublisher, InMemoryEventPublisher>();
        return services;
    }
}
