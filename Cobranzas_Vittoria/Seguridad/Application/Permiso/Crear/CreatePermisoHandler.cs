using Cobranzas_Vittoria.Seguridad.Domain.Persistence;

namespace Cobranzas_Vittoria.Seguridad.Application.Permiso.Crear;

public class CreatePermisoHandler
{
    private readonly IPermisoRepository _permisoRepository;

    public CreatePermisoHandler(IPermisoRepository permisoRepository)
    {
        _permisoRepository = permisoRepository;
    }

    public async Task<CreatePermisoResult> HandleAsync(CreatePermisoCommand command)
    {
        CreatePermisoValidator.ValidarCreate(command);

        Domain.Model.Permiso permiso = Domain.Model.Permiso.Crear(
            command.Codigo,
            command.Nombre,
            command.Descripcion);

        Domain.Model.Permiso permisoCreado = await _permisoRepository.AddAsync(permiso);

        return new CreatePermisoResult(
            permisoCreado.IdPermiso,
            permisoCreado.Codigo,
            permisoCreado.Nombre,
            permisoCreado.Descripcion,
            permisoCreado.Activo
        );
    }
}