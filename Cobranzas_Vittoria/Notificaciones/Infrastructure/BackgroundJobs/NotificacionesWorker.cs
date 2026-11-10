using System.Diagnostics;
using Cobranzas_Vittoria.Notificaciones.Application;
using Cobranzas_Vittoria.Notificaciones.Application.ProcesarNotificacionesPendientes;
using Cobranzas_Vittoria.Notificaciones.Application.RecuperarNotificacionesInterrumpidas;
using Cobranzas_Vittoria.Notificaciones.Domain.Exception;
using Microsoft.Extensions.Options;

namespace Cobranzas_Vittoria.Notificaciones.Infrastructure.BackgroundJobs;

/// <summary>Un ciclo secuencial por instancia. Una suspensión operativa requiere reiniciar la API.</summary>
public sealed class NotificacionesWorker(
    IServiceScopeFactory scopes,
    IOptions<NotificacionesWorkerOptions> options,
    TimeProvider timeProvider,
    ILogger<NotificacionesWorker> logger) : BackgroundService
{
    public bool Suspendido { get; private set; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // No bloquear el arranque de la API con el primer acceso a SQL.
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            NotificacionesWorkerOptions configuracion = options.Value;

            if (configuracion.Habilitado && !Suspendido)
            {
                Stopwatch duracion = Stopwatch.StartNew();
                try
                {
                    await using AsyncServiceScope scope = scopes.CreateAsyncScope();
                    RecuperarNotificacionesInterrumpidasHandler recuperar =
                        scope.ServiceProvider.GetRequiredService<RecuperarNotificacionesInterrumpidasHandler>();
                    ProcesarNotificacionesPendientesHandler procesar =
                        scope.ServiceProvider.GetRequiredService<ProcesarNotificacionesPendientesHandler>();

                    ResultadoProcesamientoLote recuperadas = await recuperar.HandleAsync(stoppingToken);
                    ResultadoProcesamientoLote procesadas = await procesar.HandleAsync(stoppingToken);

                    logger.LogInformation("Ciclo notificaciones: recuperadas {Recuperadas}, evaluadas {Evaluadas}, " +
                        "guardadas {Guardadas}, conflictos {Conflictos}, duración {DuracionMs} ms",
                        recuperadas.Guardadas, procesadas.Evaluadas, procesadas.Guardadas,
                        recuperadas.ConflictosConcurrencia + procesadas.ConflictosConcurrencia, duracion.ElapsedMilliseconds);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (EnvioCorreoException error) when (error.RequiereIntervencion)
                {
                    Suspendido = true;
                    logger.LogError("Notificaciones suspendidas por un fallo operativo SMTP. " +
                        "Corregir la configuración y reiniciar la API para reanudar. " +
                        "Los intentos reclamados se recuperarán por timeout.");
                }
                catch (System.Exception error)
                {
                    // No serializar excepciones: SQL y SMTP pueden incluir datos sensibles.
                    logger.LogError("Falló el ciclo de notificaciones, tipo {TipoError}, duración {DuracionMs} ms",
                        error.GetType().Name, duracion.ElapsedMilliseconds);
                }
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(configuracion.IntervaloSegundos), timeProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
