using Cobranzas_Vittoria.Contable.GastosDirectos.Application.Common;
using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Persistence;
using Cobranzas_Vittoria.Contable.GastosDirectos.Infrastructure.Almacenamiento;
using Cobranzas_Vittoria.Contable.GastosDirectos.Infrastructure.Persistence.Repository;

namespace Cobranzas_Vittoria.Contable.GastosDirectos;

/// <summary>Composición del módulo de gastos directos: puertos, adaptadores y casos de uso.</summary>
public static class GastosDirectosModule
{
    public static IServiceCollection AddGastosDirectos(this IServiceCollection services)
    {
        services.AddScoped<IGastoDirectoRepository, GastoDirectoRepository>();
        services.AddScoped<IAlmacenDocumentos, AlmacenDocumentosLocal>();

        var handlers = typeof(GastosDirectosModule).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                && t.Namespace?.StartsWith("Cobranzas_Vittoria.Contable.GastosDirectos.Application", StringComparison.Ordinal) == true
                && t.Name.EndsWith("Handler", StringComparison.Ordinal));
        foreach (var handler in handlers)
            services.AddScoped(handler);

        return services;
    }
}
