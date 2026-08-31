using Microsoft.AspNetCore.Mvc;
using Cobranzas_Vittoria.Seguridad.Presentation.Dto;

namespace Cobranzas_Vittoria.Seguridad.Presentation.Controller;

[ApiController]
[Route("api/seguridad/permisos")]
public class PermisoController : ControllerBase
{
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
    public IActionResult Add([FromBody] CreatePermisoRequest request)
    {
        // TODO: Implementar la lógica para agregar un nuevo permiso
        return Ok();
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