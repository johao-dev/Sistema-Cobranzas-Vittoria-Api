using System.Globalization;
using Cobranzas_Vittoria.Application.Common.Eventos;
using Cobranzas_Vittoria.ControlPresupuestario.Contracts.Eventos;
using Cobranzas_Vittoria.Notificaciones.Application.ProcesarEvento;

namespace Cobranzas_Vittoria.Notificaciones.Application.PresupuestoAprobado;

public sealed class PresupuestoAprobadoEventHandler(
    IConsultaPresupuestoAprobado consulta,
    ProcesarEventoHandler procesarEvento,
    ILogger<PresupuestoAprobadoEventHandler> logger) : IEventHandler<PresupuestoAprobadoEvent>
{
    private static readonly TimeZoneInfo ZONA_LIMA = TimeZoneInfo.FindSystemTimeZoneById("America/Lima");

    public async Task HandleAsync(PresupuestoAprobadoEvent evento, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evento);
        cancellationToken.ThrowIfCancellationRequested();

        DatosPresupuestoAprobado datos = await consulta.ObtenerAsync(evento.IdPresupuestoVersion, cancellationToken);
        DateTime fechaLima = TimeZoneInfo.ConvertTimeFromUtc(evento.FechaAprobacionUtc, ZONA_LIMA);

        ProcesarEventoCommand command = new(evento.IdEvento, "PRESUPUESTO_APROBADO", evento.IdPresupuestoVersion,
            new Dictionary<string, string?>
            {
                ["CodigoPresupuesto"] = datos.CodigoPresupuesto,
                ["NombrePresupuesto"] = datos.NombrePresupuesto,
                ["NumeroVersion"] = evento.NumeroVersion.ToString(CultureInfo.InvariantCulture),
                ["Proyecto"] = datos.Proyecto ?? "Sin proyecto asociado",
                ["Fecha"] = fechaLima.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                ["MotivoCambio"] = string.IsNullOrWhiteSpace(datos.MotivoCambio) ? "No especificado" : datos.MotivoCambio
            });

        ProcesarEventoResult resultado = await procesarEvento.HandleAsync(command, cancellationToken);

        logger.LogInformation(
            "Evento {IdEvento} de la versión de presupuesto {IdPresupuestoVersion}: notificación {Estado}, ID {IdNotificacion}, motivo {MotivoOmision}",
            evento.IdEvento, evento.IdPresupuestoVersion, resultado.Estado, resultado.IdNotificacion, resultado.MotivoOmision);
    }
}
