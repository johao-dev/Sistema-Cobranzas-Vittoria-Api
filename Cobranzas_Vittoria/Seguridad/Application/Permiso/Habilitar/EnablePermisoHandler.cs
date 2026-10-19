using Cobranzas_Vittoria.Seguridad.Application.Common;
using PermisoDomain = Cobranzas_Vittoria.Seguridad.Domain.Model.Permiso;
using Cobranzas_Vittoria.Seguridad.Domain.Persistence;
using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;

namespace Cobranzas_Vittoria.Seguridad.Application.Permiso.Habilitar;

/// <summary> Handler para habilitar un permiso en el sistema de seguridad. </summary>
public class EnablePermisoHandler
{
    private readonly IPermisoRepository _permisoRepository;
    private readonly IUsuarioActualService _usuarioActualService;
    private readonly ILogger<EnablePermisoHandler> _logger;

    public EnablePermisoHandler(
        IPermisoRepository permisoRepository,
        IUsuarioActualService usuarioActualService,
        ILogger<EnablePermisoHandler> logger)
    {
        _permisoRepository = permisoRepository;
        _usuarioActualService = usuarioActualService;
        _logger = logger;
    }

    public async Task HandleAsync(EnablePermisoCommand command)
    {
        PermisoValidator.ValidarEnable(command);

        _logger.LogInformation("Habilitando permiso con IdPermiso={IdPermiso}", command.IdPermiso);

        PermisoDomain permiso = await _permisoRepository.GetByIdAsync(command.IdPermiso)
            ?? throw new ValidacionNegocioSeguridadException(
                nameof(command.IdPermiso),
                "PERMISO_NO_ENCONTRADO",
                $"No se encontro el permiso con Id {command.IdPermiso}.");

        permiso.Activar(_usuarioActualService.ObtenerUsuarioActual());

        await _permisoRepository.EnableAsync(permiso);

        _logger.LogInformation("Permiso habilitado: IdPermiso={IdPermiso}", permiso.IdPermiso);
    }
}
