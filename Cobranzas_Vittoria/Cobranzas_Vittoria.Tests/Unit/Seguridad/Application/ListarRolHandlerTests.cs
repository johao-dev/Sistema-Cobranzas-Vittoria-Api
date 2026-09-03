using Cobranzas_Vittoria.Seguridad.Application.Rol.Listar;
using Cobranzas_Vittoria.Tests.Unit.Seguridad.Stubs;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cobranzas_Vittoria.Tests.Unit.Seguridad.Application;

/// <summary>
/// Pruebas unitarias de <see cref="ListarRolHandler"/>.
/// </summary>
public class ListarRolHandlerTests
{
    private StubRolRepository _repository = null!;
    private ListarRolHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        _repository = new StubRolRepository();
        _handler = new ListarRolHandler(
            _repository,
            NullLogger<ListarRolHandler>.Instance);
    }

    [Test]
    public async Task HandleAsync_SinRoles_RetornaListaVacia()
    {
        var result = await _handler.HandleAsync(new ListarRolQuery());

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task HandleAsync_ConRolesActivos_RetornaSoloActivos()
    {
        // Arrange
        _repository.Add(1, "Rol activo", activo: true);
        _repository.Add(2, "Rol inactivo", activo: false);

        // Act
        var result = (await _handler.HandleAsync(new ListarRolQuery(Activo: true))).ToList();

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].IdRol, Is.EqualTo(1));
    }

    [Test]
    public async Task HandleAsync_ConRolesInactivos_RetornaSoloInactivos()
    {
        // Arrange
        _repository.Add(1, "Rol activo", activo: true);
        _repository.Add(2, "Rol inactivo", activo: false);

        // Act
        var result = (await _handler.HandleAsync(new ListarRolQuery(Activo: false))).ToList();

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].IdRol, Is.EqualTo(2));
    }

    [Test]
    public async Task HandleAsync_SinFiltroActivo_RetornaTodos()
    {
        // Arrange
        _repository.Add(1, "Rol activo", activo: true);
        _repository.Add(2, "Rol inactivo", activo: false);

        // Act
        var result = (await _handler.HandleAsync(new ListarRolQuery(Activo: null))).ToList();

        // Assert
        Assert.That(result, Has.Count.EqualTo(2));
    }
}
