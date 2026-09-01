using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;
using Cobranzas_Vittoria.Seguridad.Domain.Persistence;

namespace Cobranzas_Vittoria.Seguridad.Application.Permiso.Eliminar;

public class DeletePermisoHandler
{
    private readonly IPermisoRepository _permisoRepository;
    private readonly ILogger<DeletePermisoHandler> _logger;

    public DeletePermisoHandler(
        IPermisoRepository permisoRepository,
        ILogger<DeletePermisoHandler> logger)
    {
        _permisoRepository = permisoRepository;
        _logger = logger;
    }

    public async Task HandleAsync(DeletePermisoCommand command)
    {
        if (command is null)
            throw new ArgumentNullException(nameof(command));

        _logger.LogInformation(
            "Iniciando eliminacion del permiso IdPermiso={IdPermiso}",
            command.IdPermiso);

        if (command.IdPermiso <= 0)
        {
            throw new ValidacionNegocioSeguridadException(
                nameof(command.IdPermiso),
                "PERMISO_ID_INVALIDO",
                "El identificador del permiso no es valido.");
        }

        Domain.Model.Permiso? permiso = await _permisoRepository.GetByIdAsync(command.IdPermiso)
            ?? throw new ValidacionNegocioSeguridadException(
                nameof(command.IdPermiso),
                "PERMISO_NO_ENCONTRADO",
                $"No se encontro el permiso con Id {command.IdPermiso}.");

        // El borrado es fisico. El estado Activo es independiente del borrado.
        await _permisoRepository.DeleteAsync(permiso.IdPermiso);

        _logger.LogInformation(
            "Permiso eliminado exitosamente: IdPermiso={IdPermiso}, Codigo={Codigo}",
            permiso.IdPermiso,
            permiso.Codigo);
    }
}
