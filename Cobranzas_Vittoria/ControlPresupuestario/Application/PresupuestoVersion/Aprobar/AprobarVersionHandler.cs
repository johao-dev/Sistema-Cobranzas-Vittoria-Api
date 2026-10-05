using Cobranzas_Vittoria.ControlPresupuestario.Application.Common;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;
using Cobranzas_Vittoria.Seguridad.Application.Common;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.PresupuestoVersion.Aprobar;

/// <summary>
/// Aprobar es una acción de negocio: el SP valida, bloquea y en una sola transacción pasa la
/// versión APROBADA anterior a HISTORICO y esta de BORRADOR a APROBADO.
/// </summary>
public sealed class AprobarVersionHandler
{
    private readonly IPresupuestoVersionRepository _repository;
    private readonly IUsuarioActualService _usuarioActual;
    private readonly ILogger<AprobarVersionHandler> _logger;

    public AprobarVersionHandler(IPresupuestoVersionRepository repository, IUsuarioActualService usuarioActual,
        ILogger<AprobarVersionHandler> logger)
    {
        _repository = repository;
        _usuarioActual = usuarioActual;
        _logger = logger;
    }

    public async Task<PresupuestoVersionResult> HandleAsync(AprobarVersionCommand command)
    {
        PresupuestoVersionValidator.ValidarIds(command.IdPresupuesto, command.IdPresupuestoVersion);
        await _repository.ObtenerDelPresupuestoAsync(command.IdPresupuesto, command.IdPresupuestoVersion);
        await _repository.AprobarAsync(command.IdPresupuestoVersion, _usuarioActual.ObtenerUsuarioActual());
        _logger.LogInformation("Versión aprobada: IdPresupuesto={Presupuesto}, IdVersion={Version}",
            command.IdPresupuesto, command.IdPresupuestoVersion);
        var aprobada = await _repository.ObtenerDelPresupuestoAsync(command.IdPresupuesto, command.IdPresupuestoVersion);
        return PresupuestoVersionResult.Desde(aprobada);
    }
}
