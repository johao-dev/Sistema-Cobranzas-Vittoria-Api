using Cobranzas_Vittoria.Seguridad.Application.Rol.Obtener;
using Cobranzas_Vittoria.Tests.Unit.Seguridad.Stubs;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cobranzas_Vittoria.Tests.Unit.Seguridad.Application;

/// <summary>
/// Pruebas unitarias de <see cref="ObtenerRolHandler"/>.
/// </summary>
public class ObtenerRolHandlerTests
{
    private StubRolRepository _repository = null!;
    private ObtenerRolHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        _repository = new StubRolRepository();
        _handler = new ObtenerRolHandler(
            _repository,
            NullLogger<ObtenerRolHandler>.Instance);
    }

    [Test]
    public async Task HandleAsync_RolExistente_RetornaRolMapeado()
    {
        // Arrange
        _repository.Add(3, "Rol leer", "Permite leer");
        var query = new ObtenerRolQuery(3);

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        Assert.That(result.IdRol, Is.EqualTo(3));
        Assert.That(result.Nombre, Is.EqualTo("Rol leer"));
        Assert.That(result.Descripcion, Is.EqualTo("Permite leer"));
    }

    [Test]
    public void HandleAsync_RolInexistente_LanzaKeyNotFoundException()
    {
        var query = new ObtenerRolQuery(999);

        var ex = Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            await _handler.HandleAsync(query))!;

        Assert.That(ex.Message, Does.Contain("999"));
    }
}
