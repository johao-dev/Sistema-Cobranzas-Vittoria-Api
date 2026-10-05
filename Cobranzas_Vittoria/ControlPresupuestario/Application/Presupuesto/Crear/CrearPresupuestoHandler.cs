using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;
using Cobranzas_Vittoria.Seguridad.Application.Common;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.Presupuesto.Crear;

public sealed class CrearPresupuestoHandler
{
    private readonly IPresupuestoRepository _repository;
    private readonly IUsuarioActualService _usuarioActual;
    private readonly ILogger<CrearPresupuestoHandler> _logger;

    public CrearPresupuestoHandler(IPresupuestoRepository repository, IUsuarioActualService usuarioActual,
        ILogger<CrearPresupuestoHandler> logger)
    {
        _repository = repository;
        _usuarioActual = usuarioActual;
        _logger = logger;
    }

    public async Task<CrearPresupuestoResult> HandleAsync(CrearPresupuestoCommand command)
    {
        var presupuesto = Domain.Model.Presupuesto.Crear(command.IdCentroCosto, command.IdMoneda, command.Codigo,
            command.Nombre, command.Descripcion, command.FechaInicio, command.FechaFin);
        var creado = await _repository.CrearAsync(presupuesto, _usuarioActual.ObtenerUsuarioActual());
        _logger.LogInformation("Presupuesto creado: IdPresupuesto={Id}, Codigo={Codigo}, IdVersion={Version}",
            creado.IdPresupuesto, presupuesto.Codigo, creado.IdPresupuestoVersion);
        return new CrearPresupuestoResult(creado.IdPresupuesto, creado.IdPresupuestoVersion, creado.NumeroVersion, creado.Estado);
    }
}
