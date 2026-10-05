using Cobranzas_Vittoria.ControlPresupuestario.Application.Common;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.Presupuesto.Actualizar;

/// <summary>
/// Actualiza un presupuesto. Inactivar uno con gastos, compromisos o requerimientos asociados exige
/// ConfirmarInactivacion: sin ella responde 409 PRESUPUESTO_CON_REGISTROS explicando qué se afecta.
/// </summary>
public sealed class ActualizarPresupuestoHandler
{
    private readonly IPresupuestoRepository _repository;
    private readonly ILogger<ActualizarPresupuestoHandler> _logger;

    public ActualizarPresupuestoHandler(IPresupuestoRepository repository, ILogger<ActualizarPresupuestoHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<PresupuestoResult> HandleAsync(ActualizarPresupuestoCommand command)
    {
        PresupuestoValidator.ValidarId(command.IdPresupuesto);
        var presupuesto = await _repository.ObtenerAsync(command.IdPresupuesto)
            ?? throw new PresupuestoNoEncontradoException(command.IdPresupuesto);
        if (presupuesto.Activo && !command.Activo && !command.ConfirmarInactivacion)
        {
            var registros = await _repository.ContarRegistrosAsociadosAsync(command.IdPresupuesto);
            if (registros.Alguno)
                throw new ControlPresupuestarioException("PRESUPUESTO_CON_REGISTROS",
                    $"Este presupuesto tiene registros asociados ({registros.GastosDirectos} gasto(s) directo(s) vigentes, " +
                    $"{registros.Movimientos} movimiento(s) presupuestal(es) y {registros.LineasRequerimiento} línea(s) de requerimiento). " +
                    "Al inactivarlo dejará de aparecer en los reportes y no admitirá nuevos gastos; " +
                    "solo se podrán anular o liberar los existentes. Confirma para continuar.");
        }
        presupuesto.Actualizar(command.Nombre, command.Activo, command.Descripcion, command.FechaInicio, command.FechaFin);
        await _repository.ActualizarAsync(presupuesto);
        _logger.LogInformation("Presupuesto actualizado: IdPresupuesto={Id}", command.IdPresupuesto);
        var actualizado = await _repository.ObtenerAsync(command.IdPresupuesto)
            ?? throw new PresupuestoNoEncontradoException(command.IdPresupuesto);
        return PresupuestoResult.Desde(actualizado);
    }
}
