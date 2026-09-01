using Cobranzas_Vittoria.Seguridad.Application.Common;

namespace Cobranzas_Vittoria.Seguridad.Infrastructure.Services;

/// <summary>
/// Implementacion default de <see cref="IUsuarioActualService"/>.
/// Devuelve "sistema" hasta que se implemente la resolucion real desde
/// HttpContext.User o el contexto de autenticacion.
/// </summary>
public sealed class UsuarioActualService : IUsuarioActualService
{
    public string ObtenerUsuarioActual() => "sistema";
}
