using System.Globalization;
using Cobranzas_Vittoria.Application.Common.Eventos;
using Cobranzas_Vittoria.Compras.Contracts.Eventos;
using Cobranzas_Vittoria.Notificaciones.Application.Options;
using Cobranzas_Vittoria.Notificaciones.Application.ProcesarEvento;
using Microsoft.Extensions.Options;

namespace Cobranzas_Vittoria.Notificaciones.Application.RequerimientoCreado;

public sealed class RequerimientoCreadoEventHandler(
    IConsultaRequerimientoCreado consulta,
    ProcesarEventoHandler procesarEvento,
    IOptions<NotificacionEnlacesOptions> enlaces,
    ILogger<RequerimientoCreadoEventHandler> logger) : IEventHandler<RequerimientoCreadoEvent>
{
    public async Task HandleAsync(RequerimientoCreadoEvent evento, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evento);
        cancellationToken.ThrowIfCancellationRequested();

        string url = enlaces.Value.ConstruirUrlRequerimientos();
        DatosRequerimientoCreado datos = await consulta.ObtenerAsync(evento.IdRequerimiento, cancellationToken);

        ProcesarEventoCommand command = new(evento.IdEvento, "REQUERIMIENTO_CREADO", evento.IdRequerimiento,
            new Dictionary<string, string?>
            {
                ["NumeroRequerimiento"] = evento.NumeroRequerimiento,
                ["Proyecto"] = datos.Proyecto,
                ["Solicitante"] = datos.Solicitante,
                ["Fecha"] = evento.FechaRequerimiento.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                ["Url"] = url
            });

        ProcesarEventoResult resultado = await procesarEvento.HandleAsync(command, cancellationToken);

        // El emisor registra los errores una sola vez y decide si afectan su resultado de negocio.
        logger.LogInformation(
            "Evento {IdEvento} del requerimiento {IdRequerimiento}: notificación {Estado}, ID {IdNotificacion}, motivo {MotivoOmision}",
            evento.IdEvento, evento.IdRequerimiento, resultado.Estado, resultado.IdNotificacion, resultado.MotivoOmision);
    }
}
