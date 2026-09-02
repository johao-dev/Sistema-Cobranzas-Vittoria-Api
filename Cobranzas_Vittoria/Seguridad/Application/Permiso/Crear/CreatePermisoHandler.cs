using Cobranzas_Vittoria.Seguridad.Application.Common;
using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;
using Cobranzas_Vittoria.Seguridad.Domain.Persistence;

namespace Cobranzas_Vittoria.Seguridad.Application.Permiso.Crear;

public class CreatePermisoHandler
{
    private readonly IPermisoRepository _permisoRepository;
    private readonly IUsuarioActualService _usuarioActualService;
    private readonly ILogger<CreatePermisoHandler> _logger;

    public CreatePermisoHandler(
        IPermisoRepository permisoRepository,
        IUsuarioActualService usuarioActualService,
        ILogger<CreatePermisoHandler> logger)
    {
        _permisoRepository = permisoRepository;
        _usuarioActualService = usuarioActualService;
        _logger = logger;
    }

    public async Task<CreatePermisoResult> HandleAsync(CreatePermisoCommand command)
    {
        _logger.LogInformation("Iniciando creacion de permiso con codigo {Codigo}", command.Codigo);
        _logger.LogDebug("Datos recibidos para crear permiso: {@Command}", command);

        PermisoValidator.ValidarCreate(command);
        if (await _permisoRepository.GetByCodigoAsync(command.Codigo.Trim()) is not null)
            throw new ValidacionNegocioSeguridadException(
                nameof(command.Codigo),
                "PERMISO_CODIGO_DUPLICADO",
                $"Ya existe un permiso con el codigo {command.Codigo}");

        Domain.Model.Permiso permiso = Domain.Model.Permiso.Crear(
            command.Codigo,
            command.Nombre,
            command.Descripcion);

        permiso.EstablecerAuditoriaCreacion(_usuarioActualService.ObtenerUsuarioActual());

        Domain.Model.Permiso permisoCreado = await _permisoRepository.AddAsync(permiso);

        _logger.LogInformation(
            "Permiso creado exitosamente: IdPermiso={IdPermiso}, Codigo={Codigo}",
            permisoCreado.IdPermiso,
            permisoCreado.Codigo);

        return new CreatePermisoResult(
            permisoCreado.IdPermiso,
            permisoCreado.Codigo,
            permisoCreado.Nombre,
            permisoCreado.Descripcion,
            permisoCreado.Activo,
            permisoCreado.FechaCreacion,
            permisoCreado.UsuarioCreacion);
    }
}