using Cobranzas_Vittoria.Seguridad.Application.Common;
using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;
using Cobranzas_Vittoria.Seguridad.Domain.Persistence;

namespace Cobranzas_Vittoria.Seguridad.Application.Rol.Actualizar;

public class ActualizarRolHandler
{
    private readonly IRolRepository _rolRepository;
    private readonly IUsuarioActualService _usuarioActualService;
    private readonly ILogger<ActualizarRolHandler> _logger;

    public ActualizarRolHandler(
        IRolRepository rolRepository,
        IUsuarioActualService usuarioActualService,
        ILogger<ActualizarRolHandler> logger)
    {
        _rolRepository = rolRepository;
        _usuarioActualService = usuarioActualService;
        _logger = logger;
    }

    public async Task<ActualizarRolResult> HandleAsync(ActualizarRolCommand command)
    {
        _logger.LogInformation(
            "Iniciando actualizacion del rol IdRol={IdRol}",
            command.IdRol);
        _logger.LogDebug("Datos recibidos para actualizar rol: {@Command}", command);

        RolValidator.ValidarUpdate(command);

        Domain.Model.Rol? rol = await _rolRepository.GetByIdAsync(command.IdRol)
            ?? throw new ValidacionNegocioSeguridadException(
                nameof(command.IdRol),
                "ROL_NO_ENCONTRADO",
                $"No se encontro el rol con Id {command.IdRol}.");

        // Actualizacion parcial: solo se aplican los valores proporcionados.
        string nombre = command.Nombre ?? rol.Nombre;
        string descripcion = command.Descripcion ?? rol.Descripcion;

        rol.ActualizarDatos(nombre, descripcion);

        if (command.Activo.HasValue)
        {
            if (command.Activo.Value)
            {
                rol.Activar();
            }
            else
            {
                rol.Desactivar();
            }
        }

        rol.EstablecerAuditoriaModificacion(_usuarioActualService.ObtenerUsuarioActual());

        Domain.Model.Rol actualizado = await _rolRepository.UpdateAsync(rol);

        _logger.LogInformation(
            "Rol actualizado exitosamente: IdRol={IdRol}",
            actualizado.IdRol);

        return new ActualizarRolResult(
            actualizado.IdRol,
            actualizado.Nombre,
            actualizado.Descripcion,
            actualizado.Activo,
            actualizado.FechaModificacion,
            actualizado.UsuarioModificacion);
    }
}
