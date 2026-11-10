using Cobranzas_Vittoria.Notificaciones.Domain.Exception;
using Cobranzas_Vittoria.Notificaciones.Domain.Model;
using NUnit.Framework;
using static Cobranzas_Vittoria.Notificaciones.Domain.Model.NotificacionDestinatario;

namespace Cobranzas_Vittoria.Tests.Unit.Notificaciones.Domain;

public class ConfiguracionNotificacionTests
{
    [Test]
    public void DesactivarReglaOPlantilla_ImpideSuUsoHastaReactivarlas()
    {
        ReglaNotificacion regla = ReglaNotificacion.Crear(1, "  Requerimiento creado  ");
        PlantillaCorreo plantilla = PlantillaCorreo.Crear(1, "  Nuevo {{Numero}}  ", " <p>{{Numero}}</p> ");

        Assert.Multiple(() =>
        {
            Assert.That(regla.PermiteNotificar(), Is.True);
            Assert.That(plantilla.PuedeUtilizarse(), Is.True);
            Assert.That(regla.Nombre, Is.EqualTo("Requerimiento creado"));
            Assert.That(plantilla.Asunto, Is.EqualTo("Nuevo {{Numero}}"));
            Assert.That(plantilla.CuerpoHtml, Is.EqualTo(" <p>{{Numero}}</p> "));
        });

        regla.Desactivar();
        plantilla.Desactivar();

        Assert.Multiple(() =>
        {
            Assert.That(regla.PermiteNotificar(), Is.False);
            Assert.That(plantilla.PuedeUtilizarse(), Is.False);
        });

        regla.Activar();
        plantilla.Activar();

        Assert.Multiple(() =>
        {
            Assert.That(regla.PermiteNotificar(), Is.True);
            Assert.That(plantilla.PuedeUtilizarse(), Is.True);
        });
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void CrearConfiguracion_ConEventoInvalido_Rechaza(int idEvento)
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<ValidacionNegocioNotificacionException>(() => ReglaNotificacion.Crear(idEvento, "Regla"));
            Assert.Throws<ValidacionNegocioNotificacionException>(() => PlantillaCorreo.Crear(idEvento, "Asunto", "<p>Cuerpo</p>"));
        });
    }

    [TestCase(null)]
    [TestCase(" ")]
    public void ActualizarPlantilla_ConCuerpoVacio_NoModificaElAsuntoAnterior(string? cuerpo)
    {
        PlantillaCorreo plantilla = PlantillaCorreo.Crear(1, "Original", "<p>Original</p>");

        Assert.Throws<ValidacionNegocioNotificacionException>(() => plantilla.ActualizarContenido("Nuevo", cuerpo!));

        Assert.Multiple(() =>
        {
            Assert.That(plantilla.Asunto, Is.EqualTo("Original"));
            Assert.That(plantilla.CuerpoHtml, Is.EqualTo("<p>Original</p>"));
        });
    }

    [TestCase(250, true)]
    [TestCase(251, false)]
    public void Plantilla_RespetaElLimiteDelAsunto(int longitud, bool permitido)
    {
        PlantillaCorreo plantilla = PlantillaCorreo.Crear(1, "Original", "<p>Original</p>");
        string asunto = new('a', longitud);

        if (permitido)
        {
            plantilla.ActualizarContenido(asunto, "<p>Nuevo</p>");
            Assert.That(plantilla.Asunto, Is.EqualTo(asunto));
            return;
        }

        Assert.Throws<ValidacionNegocioNotificacionException>(() => plantilla.ActualizarContenido(asunto, "<p>Nuevo</p>"));
        Assert.That(plantilla.Asunto, Is.EqualTo("Original"));
    }

    [TestCase(150, true)]
    [TestCase(151, false)]
    public void CambiarNombreRegla_RespetaElLimiteYNoDejaCambiosParciales(int longitud, bool permitido)
    {
        ReglaNotificacion regla = ReglaNotificacion.Crear(1, "Original");
        string nombre = new('n', longitud);

        if (permitido)
        {
            regla.CambiarNombre(nombre);
            Assert.That(regla.Nombre, Is.EqualTo(nombre));
            return;
        }

        Assert.Throws<ValidacionNegocioNotificacionException>(() => regla.CambiarNombre(nombre));
        Assert.That(regla.Nombre, Is.EqualTo("Original"));
    }

    [TestCase(TipoDestinatario.Rol, null)]
    [TestCase(TipoDestinatario.Usuario, "0")]
    [TestCase(TipoDestinatario.Rol, "-2")]
    [TestCase(TipoDestinatario.Usuario, "abc")]
    [TestCase(TipoDestinatario.Rol, "2147483648")]
    public void CrearInstruccion_ConValorIncompatibleConSuEstrategia_Rechaza(TipoDestinatario tipo, string? valor)
    {
        Assert.Throws<ValidacionNegocioNotificacionException>(() => ReglaDestinatario.Crear(10, tipo, valor, TipoEnvioConfig.To));
    }

    [Test]
    public void AgregarInstruccionDuplicada_RechazaInclusoConCerosIniciales()
    {
        ReglaNotificacion regla = ReglaNotificacion.Crear(1, "Requerimiento creado");
        ReglaDestinatario destinatario = ReglaDestinatario.Crear(10, TipoDestinatario.Rol, " 002 ", TipoEnvioConfig.To);
        regla.AgregarDestinatario(destinatario);

        Assert.Throws<ValidacionNegocioNotificacionException>(() =>
            regla.AgregarDestinatario(ReglaDestinatario.Crear(10, TipoDestinatario.Rol, "2", TipoEnvioConfig.To)));

        regla.AgregarDestinatario(ReglaDestinatario.Crear(10, TipoDestinatario.Rol, "2", TipoEnvioConfig.Cc));

        Assert.Multiple(() =>
        {
            Assert.That(regla.Destinatarios, Has.Count.EqualTo(2));
            Assert.That(destinatario.Valor, Is.EqualTo("2"));
            Assert.That(regla.QuitarDestinatario(destinatario), Is.True);
            Assert.That(regla.QuitarDestinatario(destinatario), Is.False);
        });
    }

    [Test]
    public void AgregarInstruccionPersistida_NoLaApropiaDeOtraRegla()
    {
        ReglaNotificacion regla = ReglaNotificacion.Crear(1, "Regla");
        ReglaDestinatario destinatario = ReglaDestinatario.Reconstruir(4, 7, 10, TipoDestinatario.Rol, "2", TipoEnvioConfig.To);

        Assert.Throws<ValidacionNegocioNotificacionException>(() => regla.AgregarDestinatario(destinatario));
        Assert.That(regla.Destinatarios, Is.Empty);
    }

    [Test]
    public void AgregarInstruccion_ConReferenciaHistoricaEquivalente_NoDuplicaNiNormalizaElOriginal()
    {
        ReglaDestinatario original = ReglaDestinatario.Reconstruir(4, 7, 10, TipoDestinatario.Rol, " 002 ", TipoEnvioConfig.To);
        ReglaNotificacion regla = ReglaNotificacion.Reconstruir(7, 1, "Regla", true, [original]);

        Assert.Throws<ValidacionNegocioNotificacionException>(() =>
            regla.AgregarDestinatario(ReglaDestinatario.Crear(10, TipoDestinatario.Rol, "2", TipoEnvioConfig.To)));

        Assert.Multiple(() =>
        {
            Assert.That(regla.Destinatarios, Has.Count.EqualTo(1));
            Assert.That(original.Valor, Is.EqualTo(" 002 "));
        });
    }

    [Test]
    public void ReconstruirConfiguracion_ConservaDatosYCopiaLaColeccion()
    {
        ReglaDestinatario destinatario = ReglaDestinatario.Reconstruir(4, 7, 10, TipoDestinatario.Rol, " 002 ", TipoEnvioConfig.To);
        List<ReglaDestinatario> originales = [destinatario];
        ReglaNotificacion regla = ReglaNotificacion.Reconstruir(7, 1, " Regla ", false, originales);
        PlantillaCorreo plantilla = PlantillaCorreo.Reconstruir(8, 1, " Asunto ", " <p>HTML</p> ", false);
        originales.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(regla.IdReglaNotificacion, Is.EqualTo(7));
            Assert.That(regla.Nombre, Is.EqualTo(" Regla "));
            Assert.That(regla.Destinatarios.Single().Valor, Is.EqualTo(" 002 "));
            Assert.That(regla.PermiteNotificar(), Is.False);
            Assert.That(plantilla.IdPlantilla, Is.EqualTo(8));
            Assert.That(plantilla.Asunto, Is.EqualTo(" Asunto "));
            Assert.That(plantilla.PuedeUtilizarse(), Is.False);
            Assert.Throws<NotSupportedException>(() => ((ICollection<ReglaDestinatario>)regla.Destinatarios).Clear());
        });
    }
}
