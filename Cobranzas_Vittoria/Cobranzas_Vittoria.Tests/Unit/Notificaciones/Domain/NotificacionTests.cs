using Cobranzas_Vittoria.Notificaciones.Domain.Exception;
using Cobranzas_Vittoria.Notificaciones.Domain.Model;
using NUnit.Framework;
using static Cobranzas_Vittoria.Notificaciones.Domain.Model.Notificacion;

namespace Cobranzas_Vittoria.Tests.Unit.Notificaciones.Domain;

public class NotificacionTests
{
    private static readonly DateTime INSTANTE = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan INTERVALO_REINTENTO = TimeSpan.FromMinutes(5);

    [Test]
    public void Reintentar_RespetaLaFechaProgramadaYLimpiaElErrorAlEnviar()
    {
        Notificacion notificacion = CrearNotificacion();
        notificacion.IniciarProcesamiento(INSTANTE);
        notificacion.RegistrarFallo("Fallo temporal del transporte.", INSTANTE, 3, INTERVALO_REINTENTO);

        DateTime fechaReintento = INSTANTE.Add(INTERVALO_REINTENTO);

        Assert.Multiple(() =>
        {
            Assert.That(notificacion.Estado, Is.EqualTo(EstadoNotificacion.Pendiente));
            Assert.That(notificacion.Intentos, Is.EqualTo(1));
            Assert.That(notificacion.FechaInicioProcesamiento, Is.Null);
            Assert.That(notificacion.FechaProximoIntento, Is.EqualTo(fechaReintento));
            Assert.That(notificacion.PuedeProcesarse(fechaReintento.AddSeconds(-1)), Is.False);
            Assert.That(notificacion.PuedeProcesarse(fechaReintento), Is.True);
        });

        Assert.Throws<ValidacionNegocioNotificacionException>(() =>
            notificacion.IniciarProcesamiento(fechaReintento.AddSeconds(-1)));

        Assert.That(notificacion.Intentos, Is.EqualTo(1));

        notificacion.IniciarProcesamiento(fechaReintento);
        notificacion.MarcarComoEnviada(fechaReintento);

        Assert.Multiple(() =>
        {
            Assert.That(notificacion.Estado, Is.EqualTo(EstadoNotificacion.Enviada));
            Assert.That(notificacion.Intentos, Is.EqualTo(2));
            Assert.That(notificacion.FechaEnvio, Is.EqualTo(fechaReintento));
            Assert.That(notificacion.FechaInicioProcesamiento, Is.Null);
            Assert.That(notificacion.FechaProximoIntento, Is.Null);
            Assert.That(notificacion.UltimoError, Is.Null);
            Assert.That(notificacion.PuedeProcesarse(fechaReintento), Is.False);
        });
    }

    [Test]
    public void RegistrarFallo_AlAgotarIntentos_TerminaSinProgramarOtroEnvio()
    {
        Notificacion notificacion = CrearNotificacion();
        notificacion.IniciarProcesamiento(INSTANTE);

        notificacion.RegistrarFallo("No se pudo enviar.", INSTANTE, 1, INTERVALO_REINTENTO);

        Assert.Multiple(() =>
        {
            Assert.That(notificacion.Estado, Is.EqualTo(EstadoNotificacion.Error));
            Assert.That(notificacion.Intentos, Is.EqualTo(1));
            Assert.That(notificacion.UltimoError, Is.EqualTo("No se pudo enviar."));
            Assert.That(notificacion.FechaInicioProcesamiento, Is.Null);
            Assert.That(notificacion.FechaProximoIntento, Is.Null);
            Assert.That(notificacion.PuedeProcesarse(INSTANTE.AddDays(1)), Is.False);
        });
    }

    [TestCase("iniciar")]
    [TestCase("enviar")]
    [TestCase("fallar")]
    public void OperarSobreNotificacionEnviada_RechazaLaTransicionSinModificarla(string operacion)
    {
        Notificacion notificacion = CrearNotificacion();
        notificacion.IniciarProcesamiento(INSTANTE);
        notificacion.MarcarComoEnviada(INSTANTE);

        Assert.Throws<TransicionEstadoNotificacionInvalidaException>(() =>
        {
            switch (operacion)
            {
                case "iniciar":
                    notificacion.IniciarProcesamiento(INSTANTE);
                    break;
                case "enviar":
                    notificacion.MarcarComoEnviada(INSTANTE);
                    break;
                case "fallar":
                    notificacion.RegistrarFallo("Fallo.", INSTANTE, 3, INTERVALO_REINTENTO);
                    break;
            }
        });

        Assert.Multiple(() =>
        {
            Assert.That(notificacion.Estado, Is.EqualTo(EstadoNotificacion.Enviada));
            Assert.That(notificacion.FechaEnvio, Is.EqualTo(INSTANTE));
            Assert.That(notificacion.Intentos, Is.EqualTo(1));
            Assert.That(notificacion.UltimoError, Is.Null);
        });
    }

    [TestCase(0, 5, "Fallo.")]
    [TestCase(3, 0, "Fallo.")]
    [TestCase(3, 5, " ")]
    public void RegistrarFallo_ConDatosInvalidos_ConservaElProcesamiento(int maxIntentos, int minutos, string error)
    {
        Notificacion notificacion = CrearNotificacion();
        notificacion.IniciarProcesamiento(INSTANTE);

        Assert.Throws<ValidacionNegocioNotificacionException>(() =>
            notificacion.RegistrarFallo(error, INSTANTE, maxIntentos, TimeSpan.FromMinutes(minutos)));

        Assert.Multiple(() =>
        {
            Assert.That(notificacion.Estado, Is.EqualTo(EstadoNotificacion.Procesando));
            Assert.That(notificacion.FechaInicioProcesamiento, Is.EqualTo(INSTANTE));
            Assert.That(notificacion.UltimoError, Is.Null);
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void Crear_ConAsuntoVacio_RechazaConExcepcionDeNegocio(string? asunto)
    {
        Assert.Throws<ValidacionNegocioNotificacionException>(() =>
            Notificacion.Crear(Guid.NewGuid(), 1, 125, asunto!, "<p>Contenido</p>", INSTANTE));
    }

    [TestCase(250, true)]
    [TestCase(251, false)]
    public void Crear_RespetaLaLongitudMaximaDelAsunto(int longitud, bool esValido)
    {
        string asunto = new('a', longitud);

        if (esValido)
        {
            Notificacion notificacion = Notificacion.Crear(Guid.NewGuid(), 1, 125, asunto, "<p>Contenido</p>", INSTANTE);

            Assert.That(notificacion.AsuntoGenerado, Is.EqualTo(asunto));
            return;
        }

        Assert.Throws<ValidacionNegocioNotificacionException>(() =>
            Notificacion.Crear(Guid.NewGuid(), 1, 125, asunto, "<p>Contenido</p>", INSTANTE));
    }

    private static Notificacion CrearNotificacion()
    {
        return Notificacion.Crear(Guid.NewGuid(), 1, 125, "Nuevo requerimiento", "<p>Contenido</p>", INSTANTE);
    }
}
