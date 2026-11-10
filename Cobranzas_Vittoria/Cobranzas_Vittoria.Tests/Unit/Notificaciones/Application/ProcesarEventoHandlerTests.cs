using Cobranzas_Vittoria.Notificaciones.Application.ProcesarEvento;
using Cobranzas_Vittoria.Notificaciones.Application.Services;
using Cobranzas_Vittoria.Notificaciones.Domain.Exception;
using Cobranzas_Vittoria.Notificaciones.Domain.Model;
using NUnit.Framework;

namespace Cobranzas_Vittoria.Tests.Unit.Notificaciones.Application;

public class ProcesarEventoHandlerTests
{
    private ConfiguracionRepositoryStub _configuracion = null!;
    private NotificacionRepositoryStub _repository = null!;
    private DestinatarioResolverStub _resolver = null!;
    private PlantillaRendererStub _renderer = null!;
    private ProcesarEventoHandler _handler = null!;

    [SetUp]
    public void Preparar()
    {
        _configuracion = new();
        _repository = new();
        _resolver = new();
        _renderer = new();
        _handler = new(_configuracion, _repository, new DestinatarioResolver([_resolver, new EstrategiaUsuarioStub(_resolver)]), _renderer, new RelojNotificaciones());
    }

    [Test]
    public async Task EventoActivo_RegistraInstantaneaConIdentidadOriginalSinEnviarCorreo()
    {
        ProcesarEventoCommand command = DatosNotificaciones.Command();

        ProcesarEventoResult resultado = await _handler.HandleAsync(command);

        Notificacion guardada = _repository.Almacenadas.Single().Value;
        Assert.Multiple(() =>
        {
            Assert.That(resultado, Is.EqualTo(new ProcesarEventoResult(EstadoProcesarEvento.Creada, guardada.IdNotificacion)));
            Assert.That(guardada.IdEvento, Is.EqualTo(command.IdEvento));
            Assert.That(guardada.IdReferencia, Is.EqualTo(command.IdReferencia));
            Assert.That(guardada.IdCodigoEventoNotificacion, Is.EqualTo(1));
            Assert.That(guardada.FechaCreacion, Is.EqualTo(DatosNotificaciones.AHORA));
            Assert.That(guardada.Estado, Is.EqualTo(Notificacion.EstadoNotificacion.Pendiente));
            Assert.That(guardada.Intentos, Is.Zero);
            Assert.That(guardada.AsuntoGenerado, Is.EqualTo(_renderer.Contenido.Asunto));
            Assert.That(guardada.CuerpoHtmlGenerado, Is.EqualTo(_renderer.Contenido.CuerpoHtml));
            Assert.That(_renderer.PlantillaRecibida, Is.SameAs(_configuracion.Configuracion!.Plantilla));
            Assert.That(_renderer.VariablesRecibidas, Is.SameAs(command.Variables));
            Assert.That(_configuracion.CodigoConsultado, Is.EqualTo(command.CodigoEvento));
            Assert.That(_resolver.Instrucciones.Select(actual => actual.Valor), Is.EqualTo(new[] { "2", "6" }));
            Assert.That(guardada.Destinatarios.Select(actual => actual.TipoEnvio),
                Is.EqualTo(new[] { NotificacionDestinatario.TipoEnvioConfig.To, NotificacionDestinatario.TipoEnvioConfig.Cc }));
        });
    }

    [TestCase("sinEvento", MotivoOmisionEvento.EventoSinSuscripcion)]
    [TestCase("regla", MotivoOmisionEvento.ReglaDesactivada)]
    [TestCase("plantilla", MotivoOmisionEvento.PlantillaDesactivada)]
    public async Task ConfiguracionNoHabilitada_OmiteSinResolverRenderizarNiPersistir(string componente, MotivoOmisionEvento motivo)
    {
        switch (componente)
        {
            case "sinEvento": _configuracion.Configuracion = null; break;
            case "regla": _configuracion.Configuracion!.Regla!.Desactivar(); break;
            case "plantilla": _configuracion.Configuracion!.Plantilla!.Desactivar(); break;
        }

        ProcesarEventoResult resultado = await _handler.HandleAsync(DatosNotificaciones.Command());

        Assert.Multiple(() =>
        {
            Assert.That(resultado, Is.EqualTo(new ProcesarEventoResult(EstadoProcesarEvento.Omitida, null, motivo)));
            Assert.That(_repository.Registros, Is.Zero);
            Assert.That(_resolver.Instrucciones, Is.Empty);
            Assert.That(_renderer.Llamadas, Is.Zero);
        });
    }

    [TestCase("regla")]
    [TestCase("plantilla")]
    [TestCase("destinatarios")]
    public void ConfiguracionActivaIncompleta_PropagaExcepcionDeConfiguracion(string componente)
    {
        ConfiguracionNotificacion configuracion = _configuracion.Configuracion!;
        _configuracion.Configuracion = componente switch
        {
            "regla" => configuracion with { Regla = null },
            "plantilla" => configuracion with { Plantilla = null },
            _ => configuracion with { Regla = ReglaNotificacion.Crear(1, "Sin instrucciones") }
        };

        Assert.ThrowsAsync<ConfiguracionEventoIncompletaException>(async () =>
            await _handler.HandleAsync(DatosNotificaciones.Command()));
        Assert.That(_repository.Registros, Is.Zero);
    }

    [Test]
    public async Task ReglaDesactivadaConPlantillaAusente_OmitePorDesactivacionDeliberada()
    {
        _configuracion.Configuracion!.Regla!.Desactivar();
        _configuracion.Configuracion = _configuracion.Configuracion with { Plantilla = null };

        ProcesarEventoResult resultado = await _handler.HandleAsync(DatosNotificaciones.Command());

        Assert.That(resultado.MotivoOmision, Is.EqualTo(MotivoOmisionEvento.ReglaDesactivada));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SinDestinatariosValidos_UsaExcepcionDelDominio(bool devolverInvalidos)
    {
        _resolver.Resolver = _ => devolverInvalidos
            ? [new DireccionCorreo("sin-correo"), new DireccionCorreo(" ")]
            : [];

        Assert.ThrowsAsync<NotificacionSinDestinatariosException>(async () =>
            await _handler.HandleAsync(DatosNotificaciones.Command()));

        Assert.Multiple(() =>
        {
            Assert.That(_renderer.Llamadas, Is.Zero);
            Assert.That(_repository.Registros, Is.Zero);
        });
    }

    [Test]
    public async Task MultiplesRolesYUsuario_DeduplicaDireccionNormalizadaPorParticipacionYConservaCopias()
    {
        _configuracion.Configuracion!.Regla!.AgregarDestinatario(
            ReglaDestinatario.Crear(21, TipoDestinatario.Usuario, "12", NotificacionDestinatario.TipoEnvioConfig.To));
        _resolver.Resolver = instruccion => instruccion.Valor switch
        {
            "2" => [new(" almacen@example.com ", " Almacén "), new("segundo@example.com"), new("invalido")],
            "6" => [new("ALMACEN@example.com"), new("coordinador@example.com")],
            _ => [new("ALMACEN@example.com"), new("directo@example.com")]
        };

        await _handler.HandleAsync(DatosNotificaciones.Command());

        Notificacion notificacion = _repository.Almacenadas[1];
        Assert.Multiple(() =>
        {
            Assert.That(notificacion.Destinatarios, Has.Count.EqualTo(5));
            Assert.That(notificacion.Destinatarios.Count(actual => actual.TipoEnvio == NotificacionDestinatario.TipoEnvioConfig.To), Is.EqualTo(3));
            Assert.That(notificacion.Destinatarios.Count(actual => actual.TipoEnvio == NotificacionDestinatario.TipoEnvioConfig.Cc), Is.EqualTo(2));
            Assert.That(notificacion.Destinatarios.First().Correo, Is.EqualTo("almacen@example.com"));
            Assert.That(notificacion.Destinatarios.First().Nombre, Is.EqualTo("Almacén"));
            Assert.That(_resolver.Instrucciones.Last().TipoDestinatario, Is.EqualTo(TipoDestinatario.Usuario));
        });
    }

    [Test]
    public async Task UnaInstruccionSinUsuarios_NoImpideUsarDestinatariosDeOtra()
    {
        _resolver.Resolver = instruccion => instruccion.Valor == "2" ? [] : [new("coordinador@example.com")];

        await _handler.HandleAsync(DatosNotificaciones.Command());

        Assert.That(_repository.Almacenadas[1].Destinatarios.Single().TipoEnvio, Is.EqualTo(NotificacionDestinatario.TipoEnvioConfig.Cc));
    }

    [Test]
    public async Task OcurrenciaRepetida_DevuelveIdExistenteSinModificarContenidoDestinatariosNiEstado()
    {
        ProcesarEventoCommand command = DatosNotificaciones.Command();
        ProcesarEventoResult primero = await _handler.HandleAsync(command);
        Notificacion original = _repository.Almacenadas[1];
        original.IniciarProcesamiento(DatosNotificaciones.AHORA);
        original.MarcarComoEnviada(DatosNotificaciones.AHORA);
        _renderer.Contenido = new("Otro asunto", "<p>Otro contenido</p>");
        _resolver.Resolver = _ => [new("otro@example.com")];

        ProcesarEventoResult segundo = await _handler.HandleAsync(command);

        Assert.Multiple(() =>
        {
            Assert.That(segundo.Estado, Is.EqualTo(EstadoProcesarEvento.YaRegistrada));
            Assert.That(segundo.IdNotificacion, Is.EqualTo(primero.IdNotificacion));
            Assert.That(_repository.Almacenadas, Has.Count.EqualTo(1));
            Assert.That(_repository.Almacenadas[1], Is.SameAs(original));
            Assert.That(original.Estado, Is.EqualTo(Notificacion.EstadoNotificacion.Enviada));
            Assert.That(original.AsuntoGenerado, Is.EqualTo("Asunto definitivo"));
            Assert.That(original.Destinatarios.First().Correo, Is.EqualTo("usuario@example.com"));
        });
    }

    [Test]
    public void VariableRequeridaAusente_PropagaElErrorDelRendererSinRegistrar()
    {
        VariablePlantillaFaltanteException error = new("REQUERIMIENTO_CREADO", "Proyecto");
        _renderer.Error = error;

        VariablePlantillaFaltanteException? recibido = Assert.ThrowsAsync<VariablePlantillaFaltanteException>(async () =>
            await _handler.HandleAsync(DatosNotificaciones.Command() with { Variables = new Dictionary<string, string?>() }));

        Assert.That(recibido, Is.SameAs(error));
        Assert.That(_repository.Registros, Is.Zero);
    }

    [Test]
    public void EstrategiaPersistidaNoSoportada_UsaExcepcionDeConfiguracion()
    {
        ReglaDestinatario instruccion = ReglaDestinatario.Reconstruir(1, 1, 30, (TipoDestinatario)99, null, NotificacionDestinatario.TipoEnvioConfig.To);
        _configuracion.Configuracion = _configuracion.Configuracion! with
        {
            Regla = ReglaNotificacion.Reconstruir(1, 1, "Regla", true, [instruccion])
        };

        Assert.ThrowsAsync<TipoDestinatarioNoSoportadoException>(async () => await _handler.HandleAsync(DatosNotificaciones.Command()));
        Assert.That(_resolver.Instrucciones, Is.Empty);
    }

    [Test]
    public void ErrorDeNombreNoSeTrataComoCorreoAusente()
    {
        _resolver.Resolver = _ => [new("usuario@example.com", new string('a', 151))];

        ValidacionNegocioNotificacionException? error = Assert.ThrowsAsync<ValidacionNegocioNotificacionException>(async () =>
            await _handler.HandleAsync(DatosNotificaciones.Command()));

        Assert.That(error!.CodigoError, Is.EqualTo("NOTIFICACION_DESTINATARIO_NOMBRE_INVALIDO"));
    }

    [Test]
    public void ErrorDeRegistro_NoSeConvierteEnCreacionExitosa()
    {
        InvalidOperationException error = new("Persistencia indisponible");
        _repository.ErrorRegistro = error;

        Assert.That(Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _handler.HandleAsync(DatosNotificaciones.Command())), Is.SameAs(error));
    }

    [Test]
    public async Task CancellationToken_SePropagaATodasLasDependencias()
    {
        using CancellationTokenSource source = new();
        await _handler.HandleAsync(DatosNotificaciones.Command(), source.Token);

        Assert.Multiple(() =>
        {
            Assert.That(_configuracion.TokenRecibido, Is.EqualTo(source.Token));
            Assert.That(_resolver.TokenRecibido, Is.EqualTo(source.Token));
            Assert.That(_renderer.TokenRecibido, Is.EqualTo(source.Token));
            Assert.That(_repository.TokenRecibido, Is.EqualTo(source.Token));
        });
    }

    [Test]
    public void SolicitudCancelada_NoConsultaNiRegistra()
    {
        using CancellationTokenSource source = new();
        source.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(async () => await _handler.HandleAsync(DatosNotificaciones.Command(), source.Token));
        Assert.That(_configuracion.Consultas, Is.Zero);
    }

    [Test]
    public async Task OtroEvento_UtilizaConfiguracionYVariablesSinCondicionesEspecificasDeRequerimientos()
    {
        _configuracion.Configuracion = new(8, "PRESUPUESTO_APROBADO",
            ReglaNotificacion.Crear(8, "Presupuesto"), PlantillaCorreo.Crear(8, "{{Version}}", "<p>{{Monto}}</p>"));
        _configuracion.Configuracion.Regla!.AgregarDestinatario(
            ReglaDestinatario.Crear(21, TipoDestinatario.Usuario, "12", NotificacionDestinatario.TipoEnvioConfig.Bcc));
        ProcesarEventoCommand command = new(Guid.NewGuid(), "PRESUPUESTO_APROBADO", 9,
            new Dictionary<string, string?> { ["Version"] = "V3", ["Monto"] = "S/ 100" });

        await _handler.HandleAsync(command);

        Assert.That(_repository.Almacenadas[1].IdCodigoEventoNotificacion, Is.EqualTo(8));
        Assert.That(_renderer.VariablesRecibidas, Is.SameAs(command.Variables));
    }
}
