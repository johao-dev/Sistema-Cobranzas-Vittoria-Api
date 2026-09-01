using Microsoft.AspNetCore.Mvc;
using Cobranzas_Vittoria.Seguridad.Presentation.Dto;
using Cobranzas_Vittoria.Seguridad.Application.Permiso.Crear;

namespace Cobranzas_Vittoria.Seguridad.Presentation.Controller;

[ApiController]
[Route("api/seguridad/permisos")]
public class PermisoController : ControllerBase
{
    private readonly CreatePermisoHandler _createPermisoHandler;

    public PermisoController(CreatePermisoHandler createPermisoHandler)
    {
        _createPermisoHandler = createPermisoHandler;
    }

    [HttpGet("{idPermiso}")]
    public IActionResult GetById(int idPermiso)
    {
        // TODO: Implementar la lógica para obtener un permiso por su ID
        return Ok();
    }

    [HttpGet]
    public IActionResult GetAll()
    {
        // TODO: Implementar la lógica para obtener todos los permisos
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] CreatePermisoRequest request)
    {
        CreatePermisoCommand command = new CreatePermisoCommand(
            request.Codigo,
            request.Nombre,
            request.Descripcion);

        CreatePermisoResult result = await _createPermisoHandler.HandleAsync(command);
        CreatePermisoResponse response = new CreatePermisoResponse(
            result.Id,
            result.Codigo,
            result.Nombre,
            result.Descripcion,
            result.Activo
        );

        return Ok(response);
    }

    [HttpPut("{idPermiso}")]
    public IActionResult Update(int idPermiso, [FromBody] UpdatePermisoRequest request)
    {
        // TODO: Implementar la lógica para actualizar un permiso existente
        return Ok();
    }

    [HttpDelete("{idPermiso}")]
    public IActionResult Delete(int idPermiso)
    {
        // TODO: Implementar la lógica para eliminar un permiso por su ID
        return Ok();
    }
}