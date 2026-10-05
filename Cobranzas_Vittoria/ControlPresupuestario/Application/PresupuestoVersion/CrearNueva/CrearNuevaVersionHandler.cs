using Cobranzas_Vittoria.ControlPresupuestario.Application.Common;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;
using Cobranzas_Vittoria.Seguridad.Application.Common;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.PresupuestoVersion.CrearNueva;

public sealed class CrearNuevaVersionHandler
{
    private readonly IPresupuestoVersionRepository _repository;
    private readonly IUsuarioActualService _usuarioActual;
    private readonly ILogger<CrearNuevaVersionHandler> _logger;

    public CrearNuevaVersionHandler(IPresupuestoVersionRepository repository, IUsuarioActualService usuarioActual,
        ILogger<CrearNuevaVersionHandler> logger)
    {
        _repository = repository;
        _usuarioActual = usuarioActual;
        _logger = logger;
    }

    public async Task<CrearNuevaVersionResult> HandleAsync(CrearNuevaVersionCommand command)
    {
        PresupuestoValidator.ValidarId(command.IdPresupuesto);
        var descripcion = Validacion.Texto(command.Descripcion);
        var motivo = Validacion.Texto(command.MotivoCambio);
        if (descripcion is { Length: > 500 }) throw ValidacionPresupuestariaException.Longitud("Descripcion", 500);
        if (motivo is { Length: > 500 }) throw ValidacionPresupuestariaException.Longitud("MotivoCambio", 500);
        var v = await _repository.CrearNuevaAsync(command.IdPresupuesto, descripcion, motivo,
            _usuarioActual.ObtenerUsuarioActual());
        _logger.LogInformation("Versión creada: IdPresupuesto={Presupuesto}, IdVersion={Version}, Numero={Numero}",
            v.IdPresupuesto, v.IdPresupuestoVersion, v.NumeroVersion);
        return new CrearNuevaVersionResult(v.IdPresupuesto, v.IdPresupuestoVersion, v.NumeroVersion, v.Estado,
            v.IdPresupuestoVersionBase, v.CantidadDetallesCopiados);
    }
}
