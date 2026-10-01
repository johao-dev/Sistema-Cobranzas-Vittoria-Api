using Cobranzas_Vittoria.ControlPresupuestario.Application.Common;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;
using Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Importacion;
using Cobranzas_Vittoria.ControlPresupuestario.Infrastructure.Persistence.Repository;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Cobranzas_Vittoria.ControlPresupuestario;

/// <summary>
/// Composición del módulo: une los puertos (Domain/Persistence y Application/Common) con sus
/// adaptadores de Infrastructure y registra los casos de uso. Program.cs solo llama a AddControlPresupuestario().
/// </summary>
public static class ControlPresupuestarioModule
{
    public static IServiceCollection AddControlPresupuestario(this IServiceCollection services)
    {
        // Puertos de persistencia → adaptadores Dapper.
        services.AddScoped<ICentroCostoRepository, CentroCostoRepository>();
        services.AddScoped<ICatalogoPartidaRepository, CatalogoPartidaRepository>();
        services.AddScoped<IPresupuestoRepository, PresupuestoRepository>();
        services.AddScoped<IPresupuestoVersionRepository, PresupuestoVersionRepository>();
        services.AddScoped<IPresupuestoDetalleRepository, PresupuestoDetalleRepository>();
        services.AddScoped<IMovimientoPresupuestalRepository, MovimientoPresupuestalRepository>();
        services.AddScoped<IConsultaPresupuestariaRepository, ConsultaPresupuestariaRepository>();
        services.AddScoped<ICatalogoControlPresupuestarioRepository, CatalogoControlPresupuestarioRepository>();

        // Puertos de archivos → motor de importación compartido.
        services.AddScoped<CentroCostoImportProcessor>();
        services.AddScoped<CatalogoPartidaImportProcessor>();
        services.AddScoped<IImportadorMaestros, ImportadorMaestros>();
        services.AddScoped<ILectorArchivoTabular, LectorArchivoTabular>();

        services.TryAddSingleton(TimeProvider.System);

        // Casos de uso: cada Handler de Application es un servicio scoped sin interfaz, como en Seguridad.
        var handlers = typeof(ControlPresupuestarioModule).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                && t.Namespace?.StartsWith("Cobranzas_Vittoria.ControlPresupuestario.Application.", StringComparison.Ordinal) == true
                && t.Name.EndsWith("Handler", StringComparison.Ordinal));
        foreach (var handler in handlers)
            services.AddScoped(handler);

        return services;
    }
}
