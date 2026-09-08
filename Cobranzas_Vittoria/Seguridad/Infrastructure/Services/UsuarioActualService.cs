using Cobranzas_Vittoria.Seguridad.Application.Common;
using System.Security.Claims;

namespace Cobranzas_Vittoria.Seguridad.Infrastructure.Services;

/// <summary>
/// Resuelve la identidad del usuario de la solicitud HTTP actual.
/// </summary>
public sealed class UsuarioActualService : IUsuarioActualService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UsuarioActualService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Usuario => _httpContextAccessor.HttpContext?.User;

    public bool EstaAutenticado => Usuario?.Identity?.IsAuthenticated == true;

    public int? IdUsuario
    {
        get
        {
            if (!EstaAutenticado)
                return null;

            string? id = Usuario?.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(id, out int idUsuario) ? idUsuario : null;
        }
    }

    public string? UsuarioLogin => EstaAutenticado
        ? Usuario?.Identity?.Name ?? Usuario?.FindFirstValue(ClaimTypes.Name)
        : null;

    public string? Correo => EstaAutenticado
        ? Usuario?.FindFirstValue(ClaimTypes.Email)
        : null;

    /// <summary>
    /// Mantiene el contrato de auditoria existente. Las operaciones no HTTP
    /// conservan el valor historico para no introducir una identidad ficticia.
    /// </summary>
    public string ObtenerUsuarioActual() => UsuarioLogin ?? "sistema";
}
