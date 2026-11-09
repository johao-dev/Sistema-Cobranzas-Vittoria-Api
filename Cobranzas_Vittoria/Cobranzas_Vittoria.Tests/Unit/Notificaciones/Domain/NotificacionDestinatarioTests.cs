using Cobranzas_Vittoria.Notificaciones.Domain.Exception;
using Cobranzas_Vittoria.Notificaciones.Domain.Model;
using NUnit.Framework;
using static Cobranzas_Vittoria.Notificaciones.Domain.Model.NotificacionDestinatario;

namespace Cobranzas_Vittoria.Tests.Unit.Notificaciones.Domain;

public class NotificacionDestinatarioTests
{
    [TestCase("sin-arroba")]
    [TestCase("@example.com")]
    [TestCase("uno@example.com, dos@example.com")]
    [TestCase("uno@example.com;dos@example.com")]
    [TestCase("Nombre <uno@example.com>")]
    [TestCase("uno@example.com\r\nBcc: dos@example.com")]
    public void Crear_ConCorreoMalformadoOVariosDestinatarios_RechazaSinExponerLaDireccion(string correo)
    {
        ValidacionNegocioNotificacionException? error = Assert.Throws<ValidacionNegocioNotificacionException>(() =>
            NotificacionDestinatario.Crear(correo, null, TipoEnvioConfig.To));

        Assert.That(error!.Message, Does.Not.Contain(correo));
    }

    [TestCase(TipoEnvioConfig.To)]
    [TestCase(TipoEnvioConfig.Cc)]
    [TestCase(TipoEnvioConfig.Bcc)]
    public void Crear_NormalizaTextosYConservaElTipoDeEnvio(TipoEnvioConfig tipoEnvio)
    {
        NotificacionDestinatario destinatario = NotificacionDestinatario.Crear(
            "  almacen@example.com  ", "  Almacén  ", tipoEnvio);

        Assert.Multiple(() =>
        {
            Assert.That(destinatario.IdNotificacionDestinatario, Is.Zero);
            Assert.That(destinatario.IdNotificacion, Is.Zero);
            Assert.That(destinatario.Correo, Is.EqualTo("almacen@example.com"));
            Assert.That(destinatario.Nombre, Is.EqualTo("Almacén"));
            Assert.That(destinatario.TipoEnvio, Is.EqualTo(tipoEnvio));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Crear_ConNombreVacio_LoConservaComoNull(string? nombre)
    {
        NotificacionDestinatario destinatario = NotificacionDestinatario.Crear(
            "almacen@example.com", nombre, TipoEnvioConfig.To);

        Assert.That(destinatario.Nombre, Is.Null);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Crear_SinCorreo_RechazaConExcepcionDeNegocio(string? correo)
    {
        Assert.Throws<ValidacionNegocioNotificacionException>(() =>
            NotificacionDestinatario.Crear(correo!, null, TipoEnvioConfig.To));
    }

    [Test]
    public void Crear_ConTextosEnElLimiteDePersistencia_LosAceptaTrasNormalizar()
    {
        string correo = new string('a', 242) + "@example.com";
        string nombre = new('n', 150);

        NotificacionDestinatario destinatario = NotificacionDestinatario.Crear(
            $" {correo} ", $" {nombre} ", TipoEnvioConfig.To);

        Assert.Multiple(() =>
        {
            Assert.That(destinatario.Correo, Is.EqualTo(correo));
            Assert.That(destinatario.Nombre, Is.EqualTo(nombre));
        });
    }

    [TestCase(255, 150)]
    [TestCase(254, 151)]
    public void Crear_ConTextosQueSuperanLaColumna_RechazaSinExponerLosValores(int longitudCorreo, int longitudNombre)
    {
        string correo = new string('a', longitudCorreo - 12) + "@example.com";
        string nombre = new('n', longitudNombre);

        ValidacionNegocioNotificacionException? error = Assert.Throws<ValidacionNegocioNotificacionException>(() =>
            NotificacionDestinatario.Crear(correo, nombre, TipoEnvioConfig.To));

        Assert.Multiple(() =>
        {
            Assert.That(error!.Message, Does.Not.Contain(correo));
            Assert.That(error.Message, Does.Not.Contain(nombre));
        });
    }

    [TestCase(-1)]
    [TestCase(3)]
    public void Crear_ConTipoEnvioNoDefinido_Rechaza(int tipoEnvio)
    {
        Assert.Throws<ValidacionNegocioNotificacionException>(() =>
            NotificacionDestinatario.Crear("almacen@example.com", null, (TipoEnvioConfig)tipoEnvio));
    }

    [Test]
    public void Reconstruir_ConservaIdentidadesYTextosPersistidos()
    {
        NotificacionDestinatario destinatario = NotificacionDestinatario.Reconstruir(
            12, 7, " almacen@example.com ", " Almacén ", TipoEnvioConfig.Cc);

        Assert.Multiple(() =>
        {
            Assert.That(destinatario.IdNotificacionDestinatario, Is.EqualTo(12));
            Assert.That(destinatario.IdNotificacion, Is.EqualTo(7));
            Assert.That(destinatario.Correo, Is.EqualTo(" almacen@example.com "));
            Assert.That(destinatario.Nombre, Is.EqualTo(" Almacén "));
            Assert.That(destinatario.TipoEnvio, Is.EqualTo(TipoEnvioConfig.Cc));
        });
    }
}
