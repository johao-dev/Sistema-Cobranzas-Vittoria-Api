using Cobranzas_Vittoria.Dtos.Auth;
using Cobranzas_Vittoria.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Cobranzas_Vittoria.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto)
    {
        var result = await _authService.LoginAsync(dto);
        if (result is null)
            return Unauthorized(new { message = "Usuario o contraseña incorrectos." });

        return Ok(result);
    }
}
