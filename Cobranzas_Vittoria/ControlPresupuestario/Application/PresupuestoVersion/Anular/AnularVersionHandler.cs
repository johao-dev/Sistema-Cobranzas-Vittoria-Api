using Cobranzas_Vittoria.ControlPresupuestario.Application.Common;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;
using Cobranzas_Vittoria.Seguridad.Application.Common;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.PresupuestoVersion.Anular;

/// <summary>Solo se permite BORRADOR → ANULADO y no existe reapertura; el SP lo garantiza.</summary>
public sealed class AnularVersionHandler
{
    private readonly IPresupuestoVersionRepository _repository;
    private readonly IUsuarioActualService _usuarioActual;
    private readonly ILogger<AnularVersionHandler> _logger;

    public AnularVersionHandler(IPresupuestoVersionRepository repository, IUsuarioActualService usuarioActual,
        ILogger<AnularVersionHandler> logger)
    {
        _repository = repository;
        _usuarioActual = usuarioActual;
        _logger = logger;
    }

    public async Task<PresupuestoVersionResult> HandleAsync(AnularVersionCommand command)
    {
        PresupuestoVersionValidator.ValidarIds(command.IdPresupuesto, command.IdPresupuestoVersion);
        var motivo = PresupuestoVersionValidator.ValidarMotivoAnulacion(command.Motivo);
        await _repository.ObtenerDelPresupuestoAsync(command.IdPresupuesto, command.IdPresupuestoVersion);
        await _repository.AnularAsync(command.IdPresupuestoVersion, motivo, _usuarioActual.ObtenerUsuarioActual());
        _logger.LogInformation("Versión anulada: IdPresupuesto={Presupuesto}, IdVersion={Version}",
            command.IdPresupuesto, command.IdPresupuestoVersion);
        var anulada = await _repository.ObtenerDelPresupuestoAsync(command.IdPresupuesto, command.IdPresupuestoVersion);
        return PresupuestoVersionResult.Desde(anulada);
    }
}
