using Cobranzas_Vittoria.Seguridad.Application.Usuario.Obtener;
using Cobranzas_Vittoria.Tests.Unit.Seguridad.Stubs;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cobranzas_Vittoria.Tests.Unit.Seguridad.Application;

/// <summary>
/// Pruebas unitarias de <see cref="ObtenerUsuarioHandler"/>.
/// </summary>
public class ObtenerUsuarioHandlerTests
{
    private StubUsuarioRepository _repository = null!;
    private ObtenerUsuarioHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        _repository = new StubUsuarioRepository();
        _handler = new ObtenerUsuarioHandler(
            _repository,
            NullLogger<ObtenerUsuarioHandler>.Instance);
    }

    [Test]
    public async Task HandleAsync_UsuarioExistente_RetornaResultado()
    {
        // Arrange
        _repository.Add(1, "Juan", "Perez", "juan@local", "jperez");
        var query = new ObtenerUsuarioQuery(1);

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        Assert.That(result.IdUsuario, Is.EqualTo(1));
        Assert.That(result.Nombres, Is.EqualTo("Juan"));
        Assert.That(result.UsuarioLogin, Is.EqualTo("jperez"));
    }

    [Test]
    public void HandleAsync_UsuarioInexistente_LanzaKeyNotFoundException()
    {
        var query = new ObtenerUsuarioQuery(999);

        var ex = Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            await _handler.HandleAsync(query))!;

        Assert.That(ex.Message, Does.Contain("999"));
    }
}
