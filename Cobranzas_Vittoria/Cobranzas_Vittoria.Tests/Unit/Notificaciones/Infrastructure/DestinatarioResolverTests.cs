using Cobranzas_Vittoria.Notificaciones.Application.Services;
using Cobranzas_Vittoria.Notificaciones.Domain.Exception;
using Cobranzas_Vittoria.Notificaciones.Domain.Model;
using NUnit.Framework;

namespace Cobranzas_Vittoria.Tests.Unit.Notificaciones.Infrastructure;

public class DestinatarioResolverTests
{
    [Test]
    public async Task Estrategias_SeleccionaPorTipoSinDependerDelOrdenYPropagaToken()
    {
        Estrategia rol = new(TipoDestinatario.Rol);
        Estrategia usuario = new(TipoDestinatario.Usuario);
        DestinatarioResolver fachada = new([usuario, rol]);
        using CancellationTokenSource source = new();

        await fachada.ResolverAsync(Instruccion(TipoDestinatario.Rol), source.Token);
        await fachada.ResolverAsync(Instruccion(TipoDestinatario.Usuario), source.Token);

        Assert.Multiple(() =>
        {
            Assert.That(rol.Llamadas, Is.EqualTo(1));
            Assert.That(usuario.Llamadas, Is.EqualTo(1));
            Assert.That(rol.Token, Is.EqualTo(source.Token));
            Assert.That(usuario.Token, Is.EqualTo(source.Token));
        });
    }

    [Test]
    public void EstrategiasDuplicadas_SeRechazan() => Assert.Throws<InvalidOperationException>(() =>
        new DestinatarioResolver([new Estrategia(TipoDestinatario.Rol), new Estrategia(TipoDestinatario.Rol)]));

    [Test]
    public void EstrategiaAusente_SeDistingueDeResultadoVacio() => Assert.Throws<TipoDestinatarioNoSoportadoException>(() =>
        new DestinatarioResolver([]).ResolverAsync(Instruccion(TipoDestinatario.Usuario)));

    [Test]
    public void Cancelacion_NoInvocaEstrategia()
    {
        Estrategia rol = new(TipoDestinatario.Rol);
        Assert.Throws<OperationCanceledException>(() => new DestinatarioResolver([rol])
            .ResolverAsync(Instruccion(TipoDestinatario.Rol), new CancellationToken(true)));
        Assert.That(rol.Llamadas, Is.Zero);
    }

    private static ReglaDestinatario Instruccion(TipoDestinatario tipo) =>
        ReglaDestinatario.Crear(20, tipo, "2", NotificacionDestinatario.TipoEnvioConfig.To);

    private sealed class Estrategia(TipoDestinatario tipo) : IDestinatarioResolver
    {
        public TipoDestinatario Tipo => tipo;
        public int Llamadas { get; private set; }
        public CancellationToken Token { get; private set; }

        public Task<IReadOnlyCollection<DireccionCorreo>> ResolverAsync(ReglaDestinatario instruccion,
            CancellationToken cancellationToken = default)
        {
            Llamadas++;
            Token = cancellationToken;
            return Task.FromResult<IReadOnlyCollection<DireccionCorreo>>([]);
        }
    }
}
