using System.ComponentModel.DataAnnotations;
using Cobranzas_Vittoria.Notificaciones.Application.Options;
using Cobranzas_Vittoria.Notificaciones.Application.ProcesarNotificacionesPendientes;
using Cobranzas_Vittoria.Notificaciones.Application.RecuperarNotificacionesInterrumpidas;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace Cobranzas_Vittoria.Tests.Unit.Notificaciones.Application;

public class NotificacionProcesamientoOptionsTests
{
    [Test]
    public void ValoresIniciales_CumplenLaPoliticaSolicitada()
    {
        NotificacionProcesamientoOptions options = new();

        Assert.Multiple(() =>
        {
            Assert.DoesNotThrow(options.Validar);
            Assert.That(options.TamanoLote, Is.EqualTo(10));
            Assert.That(options.MaximoIntentos, Is.EqualTo(3));
            Assert.That(options.IntervaloReintentoMinutos, Is.EqualTo(5));
            Assert.That(options.TimeoutProcesamientoMinutos, Is.EqualTo(10));
        });
    }

    [TestCase("lote", 0)]
    [TestCase("lote", -1)]
    [TestCase("intentos", 0)]
    [TestCase("intentos", -1)]
    [TestCase("intervalo", 0)]
    [TestCase("intervalo", -1)]
    [TestCase("timeout", 0)]
    [TestCase("timeout", -1)]
    public void ValoresInvalidos_ImpidenConstruirAmbosHandlers(string propiedad, int valor)
    {
        NotificacionProcesamientoOptions options = new();
        switch (propiedad)
        {
            case "lote": options.TamanoLote = valor; break;
            case "intentos": options.MaximoIntentos = valor; break;
            case "intervalo": options.IntervaloReintentoMinutos = valor; break;
            case "timeout": options.TimeoutProcesamientoMinutos = valor; break;
        }

        Assert.Multiple(() =>
        {
            Assert.Throws<ValidationException>(options.Validar);
            Assert.Throws<ValidationException>(() => new ProcesarNotificacionesPendientesHandler(
                new NotificacionRepositoryStub(), new EmailSenderStub(), Options.Create(options), new RelojNotificaciones(),
                NullLogger<ProcesarNotificacionesPendientesHandler>.Instance));
            Assert.Throws<ValidationException>(() => new RecuperarNotificacionesInterrumpidasHandler(
                new NotificacionRepositoryStub(), Options.Create(options), new RelojNotificaciones(),
                NullLogger<RecuperarNotificacionesInterrumpidasHandler>.Instance));
        });
    }
}
