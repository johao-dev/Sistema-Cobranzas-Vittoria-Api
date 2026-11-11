using Cobranzas_Vittoria.Notificaciones.Application.Options;
using Cobranzas_Vittoria.Application.Common.Eventos;
using Cobranzas_Vittoria.Compras.Contracts.Eventos;
using Cobranzas_Vittoria.Notificaciones.Application.RequerimientoCreado;
using Cobranzas_Vittoria.Notificaciones.Infrastructure.Persistence;
using Cobranzas_Vittoria.Notificaciones.Application.ProcesarEvento;
using Cobranzas_Vittoria.Notificaciones.Application.ProcesarNotificacionesPendientes;
using Cobranzas_Vittoria.Notificaciones.Application.RecuperarNotificacionesInterrumpidas;
using Cobranzas_Vittoria.Notificaciones.Application.Services;
using Cobranzas_Vittoria.Notificaciones.Domain.Persistence;
using Cobranzas_Vittoria.Notificaciones.Infrastructure.BackgroundJobs;
using Cobranzas_Vittoria.Notificaciones.Infrastructure.Destinatarios;
using Cobranzas_Vittoria.Notificaciones.Infrastructure.Email;
using Cobranzas_Vittoria.Notificaciones.Infrastructure.Persistence.Repository;
using Cobranzas_Vittoria.Notificaciones.Infrastructure.Plantillas;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Cobranzas_Vittoria.Notificaciones;

public static class NotificacionesModule
{
    public static IServiceCollection AddNotificaciones(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<NotificacionEnlacesOptions>()
            .Bind(configuration.GetSection("Notificaciones:Enlaces"));
        services.AddOptions<NotificacionProcesamientoOptions>()
            .Bind(configuration.GetSection("Notificaciones:Procesamiento"))
            .ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<NotificacionesWorkerOptions>()
            .Bind(configuration.GetSection("Notificaciones:Worker"))
            .ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<SmtpOptions>().Bind(configuration.GetSection("Notificaciones:Smtp"))
            .ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<IValidateOptions<SmtpOptions>, ValidacionSmtp>();

        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<INotificacionRepository, NotificacionRepository>();
        services.AddScoped<IConfiguracionNotificacionRepository, ConfiguracionNotificacionRepository>();
        services.AddScoped<IDestinatarioResolver, DestinatarioRolResolver>();
        services.AddScoped<IDestinatarioResolver, DestinatarioUsuarioResolver>();
        services.AddScoped<DestinatarioResolver>();
        services.AddSingleton<IPlantillaRenderer, PlantillaRenderer>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<ProcesarEventoHandler>();
        services.AddScoped<IConsultaRequerimientoCreado, ConsultaRequerimientoCreado>();
        services.AddScoped<IEventHandler<RequerimientoCreadoEvent>, RequerimientoCreadoEventHandler>();
        services.AddScoped<RecuperarNotificacionesInterrumpidasHandler>();
        services.AddScoped<ProcesarNotificacionesPendientesHandler>();
        services.AddHostedService<NotificacionesWorker>();

        return services;
    }

    private sealed class ValidacionSmtp(
        IOptions<NotificacionesWorkerOptions> worker,
        IOptions<NotificacionProcesamientoOptions> procesamiento) : IValidateOptions<SmtpOptions>
    {
        public ValidateOptionsResult Validate(string? name, SmtpOptions options)
        {
            if (!worker.Value.Habilitado)
            {
                return ValidateOptionsResult.Success;
            }

            try
            {
                options.Validar();
            }
            catch (System.ComponentModel.DataAnnotations.ValidationException)
            {
                return ValidateOptionsResult.Fail("Configure SMTP completo y TLS antes de habilitar Notificaciones.");
            }

            // Cada mensaje reserva además 30s para guardar SQL y el lote dispone de 60s de margen.
            long presupuestoSegundos = ((long)options.TimeoutTotalSegundos + options.TimeoutCierreSegundos + 30)
                * procesamiento.Value.TamanoLote + 60;

            return presupuestoSegundos < (long)procesamiento.Value.TimeoutProcesamientoMinutos * 60
                ? ValidateOptionsResult.Success
                : ValidateOptionsResult.Fail("El límite del lote debe ser menor que el timeout de recuperación.");
        }
    }
}
