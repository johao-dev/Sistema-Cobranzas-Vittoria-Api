using Cobranzas_Vittoria.Notificaciones.Application.Services;
using Cobranzas_Vittoria.Notificaciones.Domain.Exception;
using Cobranzas_Vittoria.Notificaciones.Domain.Model;
using Cobranzas_Vittoria.Notificaciones.Domain.Persistence;

namespace Cobranzas_Vittoria.Notificaciones.Application.ProcesarEvento;

/// <summary>Genera la instantánea del evento sin enviar correos ni controlar la transacción del emisor.</summary>
public sealed class ProcesarEventoHandler(
    IConfiguracionNotificacionRepository configuracionRepository,
    INotificacionRepository notificacionRepository,
    IDestinatarioResolver destinatarioResolver,
    IPlantillaRenderer plantillaRenderer,
    TimeProvider timeProvider)
{
    public async Task<ProcesarEventoResult> HandleAsync(
        ProcesarEventoCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.CodigoEvento);
        ArgumentNullException.ThrowIfNull(command.Variables);
        cancellationToken.ThrowIfCancellationRequested();

        ConfiguracionNotificacion? configuracion = await configuracionRepository.ObtenerPorCodigoEventoAsync(
            command.CodigoEvento, cancellationToken);

        if (configuracion is null)
        {
            return Omitir(MotivoOmisionEvento.EventoSinSuscripcion);
        }

        ReglaNotificacion regla = configuracion.Regla
            ?? throw new ConfiguracionEventoIncompletaException(command.CodigoEvento, "regla");

        if (!regla.PermiteNotificar())
        {
            return Omitir(MotivoOmisionEvento.ReglaDesactivada);
        }

        PlantillaCorreo plantilla = configuracion.Plantilla
            ?? throw new ConfiguracionEventoIncompletaException(command.CodigoEvento, "plantilla");

        if (!plantilla.PuedeUtilizarse())
        {
            return Omitir(MotivoOmisionEvento.PlantillaDesactivada);
        }

        if (regla.Destinatarios.Count == 0)
        {
            throw new ConfiguracionEventoIncompletaException(command.CodigoEvento, "instrucciones de destinatarios");
        }

        IReadOnlyCollection<NotificacionDestinatario> destinatarios = await ResolverDestinatariosAsync(
            regla, command.CodigoEvento, cancellationToken);

        ContenidoCorreoRenderizado contenido = await plantillaRenderer.RenderizarAsync(
            command.CodigoEvento, plantilla, command.Variables, cancellationToken);

        Notificacion notificacion = Notificacion.Crear(
            command.IdEvento,
            configuracion.IdCodigoEventoNotificacion,
            command.IdReferencia,
            contenido.Asunto,
            contenido.CuerpoHtml,
            timeProvider.GetUtcNow().UtcDateTime,
            destinatarios);

        ResultadoRegistroNotificacion registro = await notificacionRepository.RegistrarSiNoExisteAsync(
            notificacion, cancellationToken);

        return new ProcesarEventoResult(
            registro.Creada ? EstadoProcesarEvento.Creada : EstadoProcesarEvento.YaRegistrada,
            registro.IdNotificacion);
    }

    private async Task<IReadOnlyCollection<NotificacionDestinatario>> ResolverDestinatariosAsync(
        ReglaNotificacion regla,
        string codigoEvento,
        CancellationToken cancellationToken)
    {
        List<NotificacionDestinatario> destinatarios = new();
        HashSet<(string Correo, NotificacionDestinatario.TipoEnvioConfig TipoEnvio)> claves = new();

        foreach (ReglaDestinatario instruccion in regla.Destinatarios)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (instruccion.TipoDestinatario is not (TipoDestinatario.Rol or TipoDestinatario.Usuario))
            {
                throw new TipoDestinatarioNoSoportadoException(instruccion.TipoDestinatario.ToString());
            }

            IReadOnlyCollection<DireccionCorreo> direcciones = await destinatarioResolver.ResolverAsync(
                instruccion, cancellationToken);

            foreach (DireccionCorreo direccion in direcciones)
            {
                NotificacionDestinatario destinatario;

                try
                {
                    destinatario = NotificacionDestinatario.Crear(direccion.Correo, direccion.Nombre, instruccion.TipoEnvio);
                }
                catch (ValidacionNegocioNotificacionException excepcion)
                    when (excepcion.CodigoError == "NOTIFICACION_DESTINATARIO_CORREO_INVALIDO")
                {
                    // Una dirección inválida no impide utilizar las otras; no repetimos la validación del dominio.
                    continue;
                }

                if (claves.Add((destinatario.Correo.ToUpperInvariant(), destinatario.TipoEnvio)))
                {
                    destinatarios.Add(destinatario);
                }
            }
        }

        if (destinatarios.Count == 0)
        {
            throw new NotificacionSinDestinatariosException(codigoEvento);
        }

        return destinatarios.AsReadOnly();
    }

    private static ProcesarEventoResult Omitir(MotivoOmisionEvento motivo)
    {
        return new ProcesarEventoResult(EstadoProcesarEvento.Omitida, null, motivo);
    }
}
