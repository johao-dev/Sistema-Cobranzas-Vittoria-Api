using Cobranzas_Vittoria.Seguridad.Application.Common;

namespace Cobranzas_Vittoria.Tests.Unit.Seguridad.Stubs;

/// <summary>
/// Stub de <see cref="IUsuarioActualService"/> que devuelve un usuario
/// configurable. Facilita tests que verifican la auditoria de creacion/modificacion.
/// </summary>
public sealed class StubUsuarioActualService : IUsuarioActualService
{
    public string UsuarioActual { get; set; } = "test-user";

    public string ObtenerUsuarioActual() => UsuarioActual;
}
