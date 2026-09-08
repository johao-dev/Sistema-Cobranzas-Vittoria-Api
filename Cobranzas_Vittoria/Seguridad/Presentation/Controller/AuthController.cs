using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Cobranzas_Vittoria.Seguridad.Application.Auth.Login;
using Cobranzas_Vittoria.Seguridad.Application.Auth.Logout;
using Cobranzas_Vittoria.Seguridad.Application.Auth.Refresh;
using Cobranzas_Vittoria.Seguridad.Presentation.Dto;

namespace Cobranzas_Vittoria.Seguridad.Presentation.Controller;

[ApiController]
[Route("api/seguridad/auth")]
public class AuthController : ControllerBase
{
    private readonly ILogger<AuthController> _logger;
    private readonly LoginHandler _loginHandler;
    private readonly RefreshHandler _refreshHandler;
    private readonly LogoutHandler _logoutHandler;

    public AuthController(ILogger<AuthController> logger, LoginHandler loginHandler,
        RefreshHandler refreshHandler, LogoutHandler logoutHandler)
    {
        _logger = logger;
        _loginHandler = loginHandler;
        _refreshHandler = refreshHandler;
        _logoutHandler = logoutHandler;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        LoginCommand command = new(request.UsernameOrEmail, request.Password);
        try
        {
            LoginResult result = await _loginHandler.HandleAsync(command);
            return Ok(new LoginResponse(result.Token, result.Expiration, result.RefreshToken));
        }
        catch (UnauthorizedAccessException) // TODO: Migrar este catch y los otros al ApiExceptionMiddleware.
        {
            return Unauthorized();
        }
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request)
    {
        await _logoutHandler.HandleAsync(request.RefreshToken);
        return NoContent();
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        try
        {
            LoginResult result = await _refreshHandler.HandleAsync(new RefreshCommand(request.RefreshToken));
            return Ok(new LoginResponse(result.Token, result.Expiration, result.RefreshToken));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }
}
