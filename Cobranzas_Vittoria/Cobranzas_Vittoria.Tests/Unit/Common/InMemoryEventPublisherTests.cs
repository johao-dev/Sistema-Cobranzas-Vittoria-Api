using Cobranzas_Vittoria.Application.Common.Eventos;
using Cobranzas_Vittoria.Infrastructure.Eventos;
using Microsoft.Extensions.DependencyInjection;

namespace Cobranzas_Vittoria.Tests.Unit.Common;

public class InMemoryEventPublisherTests
{
    [Test]
    public async Task Publicar_SinSuscriptores_CompletaNormalmente()
    {
        using ServiceProvider services = new ServiceCollection().AddEventosEnMemoria().BuildServiceProvider();
        using IServiceScope scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IEventPublisher>().PublishAsync(new Evento(1));
    }

    [Test]
    public async Task Publicar_UnSuscriptor_ConservaInstanciaYToken()
    {
        Evento evento = new(1);
        using CancellationTokenSource cts = new();
        Handler handler = new((recibido, token) =>
        {
            Assert.That(recibido, Is.SameAs(evento));
            Assert.That(token, Is.EqualTo(cts.Token));
            return Task.CompletedTask;
        });
        using ServiceProvider services = new ServiceCollection().AddEventosEnMemoria()
            .AddScoped<IEventHandler<Evento>>(_ => handler).BuildServiceProvider();
        using IServiceScope scope = services.CreateScope();

        await scope.ServiceProvider.GetRequiredService<IEventPublisher>().PublishAsync(evento, cts.Token);
        Assert.That(handler.Llamadas, Is.EqualTo(1));
    }

    [Test]
    public async Task Publicar_VariosSuscriptores_EsperaCadaUnoEnOrdenDeRegistro()
    {
        List<int> orden = [];
        TaskCompletionSource continuar = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using ServiceProvider services = new ServiceCollection().AddEventosEnMemoria()
            .AddScoped<IEventHandler<Evento>>(_ => new Handler(async (_, _) =>
            {
                orden.Add(1);
                await continuar.Task;
                orden.Add(2);
            }))
            .AddScoped<IEventHandler<Evento>>(_ => new Handler((_, _) =>
            {
                orden.Add(3);
                return Task.CompletedTask;
            })).BuildServiceProvider();
        using IServiceScope scope = services.CreateScope();
        Task publicacion = scope.ServiceProvider.GetRequiredService<IEventPublisher>().PublishAsync(new Evento(1));

        try
        {
            Assert.That(orden, Is.EqualTo(new[] { 1 }));
            Assert.That(publicacion.IsCompleted, Is.False);
        }
        finally
        {
            continuar.SetResult();
            await publicacion;
        }

        Assert.That(orden, Is.EqualTo(new[] { 1, 2, 3 }));
    }

    [Test]
    public void Publicar_FalloDeSuscriptor_PropagaMismaExcepcionYDetieneRestantes()
    {
        InvalidOperationException error = new("Fallo esperado");
        Handler segundo = new((_, _) => Task.CompletedTask);
        using ServiceProvider services = new ServiceCollection().AddEventosEnMemoria()
            .AddScoped<IEventHandler<Evento>>(_ => new Handler((_, _) => Task.FromException(error)))
            .AddScoped<IEventHandler<Evento>>(_ => segundo).BuildServiceProvider();
        using IServiceScope scope = services.CreateScope();

        Exception? recibida = Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<IEventPublisher>().PublishAsync(new Evento(1)));

        Assert.That(recibida, Is.SameAs(error));
        Assert.That(segundo.Llamadas, Is.Zero);
    }

    [Test]
    public void Publicar_Cancelado_NoInvocaSuscriptores()
    {
        using CancellationTokenSource cts = new();
        cts.Cancel();
        Handler handler = new((_, _) => Task.CompletedTask);
        using ServiceProvider services = new ServiceCollection().AddEventosEnMemoria()
            .AddScoped<IEventHandler<Evento>>(_ => handler).BuildServiceProvider();
        using IServiceScope scope = services.CreateScope();

        Assert.ThrowsAsync<OperationCanceledException>(() =>
            scope.ServiceProvider.GetRequiredService<IEventPublisher>().PublishAsync(new Evento(1), cts.Token));
        Assert.That(handler.Llamadas, Is.Zero);
    }

    [Test]
    public void Publicar_CancelacionEntreSuscriptores_DetieneEntrega()
    {
        using CancellationTokenSource cts = new();
        Handler segundo = new((_, _) => Task.CompletedTask);
        using ServiceProvider services = new ServiceCollection().AddEventosEnMemoria()
            .AddScoped<IEventHandler<Evento>>(_ => new Handler((_, _) =>
            {
                cts.Cancel();
                return Task.CompletedTask;
            }))
            .AddScoped<IEventHandler<Evento>>(_ => segundo).BuildServiceProvider();
        using IServiceScope scope = services.CreateScope();

        Assert.ThrowsAsync<OperationCanceledException>(() =>
            scope.ServiceProvider.GetRequiredService<IEventPublisher>().PublishAsync(new Evento(1), cts.Token));
        Assert.That(segundo.Llamadas, Is.Zero);
    }

    [Test]
    public async Task Publicar_UsaDependenciasDelScopeEmisor_YRespetaSuLiberacion()
    {
        ServiceCollection registros = new();
        registros.AddEventosEnMemoria().AddEventosEnMemoria();
        registros.AddScoped<DependenciaScoped>();
        registros.AddScoped<IEventHandler<Evento>, HandlerScoped>();
        using ServiceProvider services = registros.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        DependenciaScoped dependencia;

        using (IServiceScope scope = services.CreateScope())
        {
            dependencia = scope.ServiceProvider.GetRequiredService<DependenciaScoped>();
            IEventPublisher publicador = scope.ServiceProvider.GetRequiredService<IEventPublisher>();
            Assert.That(publicador, Is.SameAs(scope.ServiceProvider.GetRequiredService<IEventPublisher>()));

            await publicador.PublishAsync(new Evento(1));
            Assert.That(dependencia.Eventos, Is.EqualTo(new[] { 1 }));
            Assert.That(dependencia.Liberada, Is.False);
        }

        Assert.That(dependencia.Liberada, Is.True);
        using IServiceScope otroScope = services.CreateScope();
        Assert.That(otroScope.ServiceProvider.GetRequiredService<DependenciaScoped>(), Is.Not.SameAs(dependencia));
    }

    private sealed record Evento(int Id);

    private sealed class Handler(Func<Evento, CancellationToken, Task> accion) : IEventHandler<Evento>
    {
        public int Llamadas { get; private set; }
        public Task HandleAsync(Evento evento, CancellationToken cancellationToken = default)
        {
            Llamadas++;
            return accion(evento, cancellationToken);
        }
    }

    private sealed class DependenciaScoped : IDisposable
    {
        public List<int> Eventos { get; } = [];
        public bool Liberada { get; private set; }
        public void Dispose() => Liberada = true;
    }

    private sealed class HandlerScoped(DependenciaScoped dependencia) : IEventHandler<Evento>
    {
        public Task HandleAsync(Evento evento, CancellationToken cancellationToken = default)
        {
            dependencia.Eventos.Add(evento.Id);
            return Task.CompletedTask;
        }
    }
}
