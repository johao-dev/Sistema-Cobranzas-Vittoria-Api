using Cobranzas_Vittoria.Seguridad.Application.Common;
using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;
using Cobranzas_Vittoria.Seguridad.Domain.Persistence;

namespace Cobranzas_Vittoria.Seguridad.Application.Permiso.Actualizar;

public class UpdatePermisoHandler
{
    private readonly IPermisoRepository _permisoRepository;
    private readonly IUsuarioActualService _usuarioActualService;
    private readonly ILogger<UpdatePermisoHandler> _logger;

    public UpdatePermisoHandler(
        IPermisoRepository permisoRepository,
        IUsuarioActualService usuarioActualService,
        ILogger<UpdatePermisoHandler> logger)
    {
        _permisoRepository = permisoRepository;
        _usuarioActualService = usuarioActualService;
        _logger = logger;
    }

    public async Task HandleAsync(UpdatePermisoCommand command)
    {
        _logger.LogInformation(
            "Iniciando actualizacion del permiso IdPermiso={IdPermiso}",
            command.IdPermiso);
        _logger.LogDebug("Datos recibidos para actualizar permiso: {@Command}", command);

        UpdatePermisoValidator.ValidarUpdate(command);

        Domain.Model.Permiso? permiso = await _permisoRepository.GetByIdAsync(command.IdPermiso)
            ?? throw new ValidacionNegocioSeguridadException(
                nameof(command.IdPermiso),
                "PERMISO_NO_ENCONTRADO",
                $"No se encontro el permiso con Id {command.IdPermiso}.");

        // Actualizacion parcial: solo se aplican los valores proporcionados.
        string nombre = command.Nombre ?? permiso.Nombre;
        string descripcion = command.Descripcion ?? permiso.Descripcion;

        permiso.ActualizarDatos(nombre, descripcion);
        permiso.EstablecerAuditoriaModificacion(_usuarioActualService.ObtenerUsuarioActual());

        await _permisoRepository.UpdateAsync(permiso);

        _logger.LogInformation(
            "Permiso actualizado exitosamente: IdPermiso={IdPermiso}",
            permiso.IdPermiso);
    }
}
