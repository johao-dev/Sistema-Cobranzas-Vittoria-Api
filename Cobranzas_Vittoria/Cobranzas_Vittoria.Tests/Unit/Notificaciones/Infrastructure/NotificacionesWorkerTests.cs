using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Notificaciones;
using Cobranzas_Vittoria.Notificaciones.Application.Options;
using Cobranzas_Vittoria.Notificaciones.Application.ProcesarEvento;
using Cobranzas_Vittoria.Notificaciones.Application.Services;
using Cobranzas_Vittoria.Notificaciones.Domain.Exception;
using Cobranzas_Vittoria.Notificaciones.Domain.Model;
using Cobranzas_Vittoria.Notificaciones.Domain.Persistence;
using Cobranzas_Vittoria.Notificaciones.Infrastructure.BackgroundJobs;
using Cobranzas_Vittoria.Notificaciones.Infrastructure.Email;
using Cobranzas_Vittoria.Tests.Unit.Notificaciones.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace Cobranzas_Vittoria.Tests.Unit.Notificaciones.Infrastructure;

public class NotificacionesWorkerTests
{
    [Test]
    public async Task Composicion_ResuelveAmbasEstrategiasYTodosLosHandlersSinActivarEnvios()
    {
        await using ServiceProvider provider = CrearProvider(false);
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        IServiceProvider services = scope.ServiceProvider;

        Assert.Multiple(() =>
        {
            Assert.That(services.GetRequiredService<ProcesarEventoHandler>(), Is.Not.Null);
            Assert.That(services.GetRequiredService<DestinatarioResolver>(), Is.Not.Null);
            Assert.That(services.GetServices<IDestinatarioResolver>().Select(e => e.Tipo),
                Is.EquivalentTo(Enum.GetValues<TipoDestinatario>()));
            Assert.That(services.GetRequiredService<IConfiguracionNotificacionRepository>(), Is.Not.Null);
            Assert.That(services.GetRequiredService<IOptions<NotificacionesWorkerOptions>>().Value.Habilitado, Is.False);
            Assert.That(services.GetRequiredService<IOptions<NotificacionProcesamientoOptions>>().Value.TamanoLote, Is.EqualTo(3));
        });
        provider.GetRequiredService<IStartupValidator>().Validate();
    }

    [Test]
    public async Task WorkerDeshabilitado_NoConsultaCola()
    {
        NotificacionRepositoryStub repository = new();
        await using ServiceProvider provider = CrearProvider(false, repository);
        NotificacionesWorker worker = Worker(provider);
        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        await worker.StopAsync(CancellationToken.None);
        Assert.That(repository.Reclamaciones, Is.Zero);
    }

    [Test]
    public async Task CiclosSecuenciales_NoSeSuperponenYProcesanLoteDeTresDespuesDeRecuperar()
    {
        NotificacionRepositoryStub repository = new();
        for (int i = 1; i <= 3; i++) repository.Almacenadas.Add(i, DatosNotificaciones.Copiar(DatosNotificaciones.Pendiente(), i));
        repository.Almacenadas.Add(4, DatosNotificaciones.Copiar(DatosNotificaciones.Procesando(), 4));
        TaskCompletionSource primerEnvio = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource liberar = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource enviados = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int activos = 0;
        int maximoActivos = 0;
        int terminados = 0;
        EmailSenderStub sender = new()
        {
            AlEnviar = async (_, token) =>
            {
                maximoActivos = Math.Max(maximoActivos, ++activos);
                primerEnvio.TrySetResult();
                await liberar.Task.WaitAsync(token);
                activos--;
                if (++terminados == 3) enviados.TrySetResult();
            }
        };
        await using ServiceProvider provider = CrearProvider(true, repository, sender);
        NotificacionesWorker worker = Worker(provider);
        await worker.StartAsync(CancellationToken.None);
        try
        {
            await primerEnvio.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.That(repository.LimiteRecuperacion, Is.Not.Null);
            Assert.That(repository.Almacenadas[4].Estado, Is.EqualTo(Notificacion.EstadoNotificacion.Pendiente));
            await Task.Delay(1100);
            Assert.That(repository.Reclamaciones, Is.EqualTo(1));
            liberar.TrySetResult();
            await enviados.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Esperar(() => repository.Reclamaciones >= 2);
            Assert.That(maximoActivos, Is.EqualTo(1));
            Assert.That(repository.CantidadSolicitada, Is.EqualTo(3));
            Assert.That(repository.Almacenadas.Values.Count(n => n.Estado == Notificacion.EstadoNotificacion.Enviada), Is.EqualTo(3));
        }
        finally
        {
            liberar.TrySetResult();
            await worker.StopAsync(CancellationToken.None);
        }
    }

    [Test]
    public async Task FalloOperativo_SuspendeSinConsumirRestoDelLoteYReinicioRecuperaIntentos()
    {
        NotificacionRepositoryStub repository = new();
        for (int i = 1; i <= 3; i++) repository.Almacenadas.Add(i, DatosNotificaciones.Copiar(DatosNotificaciones.Pendiente(), i));
        EmailSenderStub sender = new()
        {
            AlEnviar = (_, _) => throw new EnvioCorreoException("Fallo operativo", false, requiereIntervencion: true)
        };
        RelojNotificaciones reloj = new();
        await using ServiceProvider provider = CrearProvider(true, repository, sender, reloj);
        NotificacionesWorker worker = Worker(provider);
        await worker.StartAsync(CancellationToken.None);
        await Esperar(() => worker.Suspendido);
        await Task.Delay(1100);
        await worker.StopAsync(CancellationToken.None);
        Assert.That(repository.Reclamaciones, Is.EqualTo(1));
        Assert.That(sender.Solicitudes.Count, Is.EqualTo(1));
        Assert.That(repository.Resultados, Is.Empty);
        Assert.That(repository.Almacenadas.Values.All(n => n.Estado == Notificacion.EstadoNotificacion.Procesando && n.Intentos == 1), Is.True);

        // Simular un proceso nuevo con la credencial corregida y el timeout ya vencido.
        reloj.AhoraUtc = DatosNotificaciones.AHORA.AddMinutes(11);
        sender.AlEnviar = null;
        using NotificacionesWorker reiniciado = new(provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IOptions<NotificacionesWorkerOptions>>(), reloj,
            NullLogger<NotificacionesWorker>.Instance);
        await reiniciado.StartAsync(CancellationToken.None);
        await Esperar(() => repository.Resultados.Count == 3);
        await reiniciado.StopAsync(CancellationToken.None);
        Assert.That(reiniciado.Suspendido, Is.False);
        Assert.That(repository.Almacenadas.Values.All(n => n.Estado == Notificacion.EstadoNotificacion.Pendiente && n.Intentos == 1), Is.True);
    }

    [Test]
    public async Task Cancelacion_DetieneEnvioYCicloSinRegistrarFallo()
    {
        NotificacionRepositoryStub repository = new();
        repository.Almacenadas.Add(1, DatosNotificaciones.Pendiente());
        TaskCompletionSource enviando = new(TaskCreationOptions.RunContinuationsAsynchronously);
        EmailSenderStub sender = new()
        {
            AlEnviar = async (_, token) =>
            {
                enviando.TrySetResult();
                await Task.Delay(Timeout.Infinite, token);
            }
        };
        await using ServiceProvider provider = CrearProvider(true, repository, sender);
        NotificacionesWorker worker = Worker(provider);
        await worker.StartAsync(CancellationToken.None);
        await enviando.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(CancellationToken.None);
        Assert.That(repository.Resultados, Is.Empty);
        Assert.That(worker.ExecuteTask!.IsCompletedSuccessfully, Is.True);
    }

    [TestCase("Notificaciones:Smtp:Host", "")]
    [TestCase("Notificaciones:Smtp:ModoTls", "99")]
    [TestCase("Notificaciones:Procesamiento:TamanoLote", "10")]
    [TestCase("Notificaciones:Worker:IntervaloSegundos", "0")]
    public async Task ConfiguracionInvalida_FallaAlIniciar(string clave, string valor)
    {
        await using ServiceProvider provider = CrearProvider(true, ajustes: new Dictionary<string, string?> { [clave] = valor });
        System.Exception? error = Assert.Catch(() => provider.GetRequiredService<IStartupValidator>().Validate());
        IEnumerable<System.Exception> errores = error is AggregateException agregado
            ? agregado.Flatten().InnerExceptions : new[] { error! };
        Assert.That(errores.All(e => e is OptionsValidationException), Is.True);
    }

    private static ServiceProvider CrearProvider(bool habilitado, NotificacionRepositoryStub? repository = null,
        EmailSenderStub? sender = null, RelojNotificaciones? reloj = null, Dictionary<string, string?>? ajustes = null)
    {
        Dictionary<string, string?> valores = new()
        {
            ["Notificaciones:Worker:Habilitado"] = habilitado.ToString(),
            ["Notificaciones:Worker:IntervaloSegundos"] = "1",
            ["Notificaciones:Smtp:Host"] = "smtp.example.invalid",
            ["Notificaciones:Smtp:Usuario"] = "usuario-ficticio",
            ["Notificaciones:Smtp:Contrasena"] = "clave-ficticia"
        };
        if (ajustes is not null) foreach (KeyValuePair<string, string?> ajuste in ajustes) valores[ajuste.Key] = ajuste.Value;
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IDbConnectionFactory, ConexionNoUtilizada>();
        services.AddSingleton<TimeProvider>(reloj ?? new RelojNotificaciones());
        services.AddNotificaciones(configuration);
        services.Replace(ServiceDescriptor.Singleton<INotificacionRepository>(repository ?? new NotificacionRepositoryStub()));
        services.Replace(ServiceDescriptor.Singleton<IEmailSender>(sender ?? new EmailSenderStub()));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    private static NotificacionesWorker Worker(ServiceProvider provider) => provider.GetServices<IHostedService>()
        .OfType<NotificacionesWorker>().Single();

    private static async Task Esperar(Func<bool> condicion)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
        while (!condicion()) await Task.Delay(10, timeout.Token);
    }

    private sealed class ConexionNoUtilizada : IDbConnectionFactory
    {
        public System.Data.IDbConnection CreateConnection() => throw new InvalidOperationException("Esta prueba no debe acceder a SQL.");
    }
}
