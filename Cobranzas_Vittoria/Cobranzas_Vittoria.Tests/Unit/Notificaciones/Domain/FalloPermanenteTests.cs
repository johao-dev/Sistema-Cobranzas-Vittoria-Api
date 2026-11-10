using Cobranzas_Vittoria.Notificaciones.Domain.Exception;
using Cobranzas_Vittoria.Notificaciones.Domain.Model;
using NUnit.Framework;

namespace Cobranzas_Vittoria.Tests.Unit.Notificaciones.Domain;

public class FalloPermanenteTests
{
    private static readonly DateTime AHORA = new(2026, 11, 10, 15, 0, 0, DateTimeKind.Utc);

    private static Notificacion Crear()
    {
        return Notificacion.Crear(Guid.NewGuid(), 1, 125, "Asunto", "<p>Contenido</p>", AHORA,
            [NotificacionDestinatario.Crear("usuario@example.com", null, NotificacionDestinatario.TipoEnvioConfig.To)]);
    }

    [Test]
    public void RegistrarFalloPermanente_TerminaElPrimerIntentoSinAlterarElContador()
    {
        Notificacion notificacion = Crear();
        notificacion.IniciarProcesamiento(AHORA);

        notificacion.RegistrarFallo("Fallo permanente", AHORA, 3, TimeSpan.FromMinutes(5), permitirReintento: false);

        Assert.Multiple(() =>
        {
            Assert.That(notificacion.Estado, Is.EqualTo(Notificacion.EstadoNotificacion.Error));
            Assert.That(notificacion.Intentos, Is.EqualTo(1));
            Assert.That(notificacion.FechaProximoIntento, Is.Null);
            Assert.That(notificacion.FechaInicioProcesamiento, Is.Null);
            Assert.That(notificacion.FechaEnvio, Is.Null);
            Assert.That(notificacion.UltimoError, Is.EqualTo("Fallo permanente"));
        });
    }

    [Test]
    public void RegistrarFalloPermanente_ExigeUnIntentoEnCurso()
    {
        Notificacion notificacion = Crear();

        Assert.Throws<TransicionEstadoNotificacionInvalidaException>(() =>
            notificacion.RegistrarFallo("Fallo", AHORA, 3, TimeSpan.FromMinutes(5), permitirReintento: false));

        Assert.That(notificacion.Estado, Is.EqualTo(Notificacion.EstadoNotificacion.Pendiente));
        Assert.That(notificacion.Intentos, Is.Zero);
    }

    [TestCase(0, 5, "Fallo")]
    [TestCase(3, 0, "Fallo")]
    [TestCase(3, 5, " ")]
    public void RegistrarFalloPermanente_NoEvadeValidacionesNiDejaCambiosParciales(int intentos, int minutos, string mensaje)
    {
        Notificacion notificacion = Crear();
        notificacion.IniciarProcesamiento(AHORA);

        Assert.Throws<ValidacionNegocioNotificacionException>(() =>
            notificacion.RegistrarFallo(mensaje, AHORA, intentos, TimeSpan.FromMinutes(minutos), permitirReintento: false));

        Assert.That(notificacion.Estado, Is.EqualTo(Notificacion.EstadoNotificacion.Procesando));
        Assert.That(notificacion.FechaInicioProcesamiento, Is.EqualTo(AHORA));
        Assert.That(notificacion.UltimoError, Is.Null);
    }

    [Test]
    public void ErrorDefinitivo_NoPermiteReactivarMedianteInicio()
    {
        Notificacion notificacion = Crear();
        notificacion.IniciarProcesamiento(AHORA);
        notificacion.RegistrarFallo("Fallo", AHORA, 3, TimeSpan.FromMinutes(5), permitirReintento: false);

        Assert.Throws<TransicionEstadoNotificacionInvalidaException>(() => notificacion.IniciarProcesamiento(AHORA.AddMinutes(5)));
        Assert.That(notificacion.Estado, Is.EqualTo(Notificacion.EstadoNotificacion.Error));
    }
}
