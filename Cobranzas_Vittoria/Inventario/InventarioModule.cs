using Cobranzas_Vittoria.Inventario.Application.Common;
using Cobranzas_Vittoria.Inventario.Application.Kardex;
using Cobranzas_Vittoria.Inventario.Domain.Persistence;
using Cobranzas_Vittoria.Inventario.Infrastructure.Persistence.Repository;

namespace Cobranzas_Vittoria.Inventario;

/// <summary>Composición explícita del módulo de Inventario.</summary>
public static class InventarioModule
{
    public static IServiceCollection AddInventario(this IServiceCollection services)
    {
        services.AddScoped<IKardexEntradaRepository, KardexEntradaRepository>();
        services.AddScoped<IKardexSalidaRepository, KardexSalidaRepository>();
        services.AddScoped<IKardexStockRepository, KardexStockRepository>();

        services.AddScoped<KardexInventarioValidator>();
        services.AddScoped<IKardexInventarioService, KardexInventarioService>();

        return services;
    }
}
