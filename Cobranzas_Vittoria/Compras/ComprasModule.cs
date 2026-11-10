using Cobranzas_Vittoria.Compras.Application.Requerimiento.Items.Actualizar;
using Cobranzas_Vittoria.Compras.Application.Requerimiento.Items.Agregar;
using Cobranzas_Vittoria.Compras.Application.Requerimiento.Items.Eliminar;
using Cobranzas_Vittoria.Compras.Application.Requerimiento.Crear;
using Cobranzas_Vittoria.Compras.Domain.Persistence;
using Cobranzas_Vittoria.Compras.Infrastructure.Persistence.Repository;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cobranzas_Vittoria.Compras;

/// <summary>Composición explícita de la parte migrada de Compras.</summary>
public static class ComprasModule
{
    public static IServiceCollection AddCompras(this IServiceCollection services)
    {
        services.AddScoped<IRequerimientoRepository, RequerimientoRepository>();
        services.AddScoped<IConsultaCreacionRequerimiento, ConsultaCreacionRequerimiento>();
        services.AddScoped<CrearRequerimientoHandler>();
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<AgregarItemHandler>();
        services.AddScoped<ActualizarItemHandler>();
        services.AddScoped<EliminarItemHandler>();

        return services;
    }
}
