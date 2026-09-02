using Cobranzas_Vittoria.Seguridad.Application.Permiso.ObtenerPorId;
using Cobranzas_Vittoria.Tests.Unit.Seguridad.Stubs;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cobranzas_Vittoria.Tests.Unit.Seguridad.Application;

/// <summary>
/// Pruebas unitarias de <see cref="ObtenerPorIdHandler"/>.
/// </summary>
public class ObtenerPorIdHandlerTests
{
    private StubPermisoRepository _repository = null!;
    private ObtenerPorIdHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        _repository = new StubPermisoRepository();
        _handler = new ObtenerPorIdHandler(
            _repository,
            NullLogger<ObtenerPorIdHandler>.Instance);
    }

    [Test]
    public async Task HandleAsync_PermisoExistente_RetornaPermisoMapeado()
    {
        // Arrange
        _repository.Add(3, "permiso.leer", "Leer", "Permite leer");
        var query = new ObtenerPorIdQuery(3);

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        Assert.That(result.IdPermiso, Is.EqualTo(3));
        Assert.That(result.Codigo, Is.EqualTo("permiso.leer"));
        Assert.That(result.Nombre, Is.EqualTo("Leer"));
        Assert.That(result.Descripcion, Is.EqualTo("Permite leer"));
    }

    [Test]
    public void HandleAsync_PermisoInexistente_LanzaKeyNotFoundException()
    {
        var query = new ObtenerPorIdQuery(999);

        var ex = Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            await _handler.HandleAsync(query))!;

        Assert.That(ex.Message, Does.Contain("999"));
    }
}
