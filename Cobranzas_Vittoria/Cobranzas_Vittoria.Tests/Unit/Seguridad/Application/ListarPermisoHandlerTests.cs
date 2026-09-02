using Cobranzas_Vittoria.Seguridad.Application.Permiso.Listar;
using Cobranzas_Vittoria.Tests.Unit.Seguridad.Stubs;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cobranzas_Vittoria.Tests.Unit.Seguridad.Application;

/// <summary>
/// Pruebas unitarias de <see cref="ListarPermisoHandler"/>.
/// </summary>
public class ListarPermisoHandlerTests
{
    private StubPermisoRepository _repository = null!;
    private ListarPermisoHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        _repository = new StubPermisoRepository();
        _handler = new ListarPermisoHandler(
            _repository,
            NullLogger<ListarPermisoHandler>.Instance);
    }

    [Test]
    public async Task HandleAsync_SinPermisos_RetornaListaVacia()
    {
        var result = await _handler.HandleAsync(new ListarPermisoQuery());

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task HandleAsync_ConPermisosActivos_RetornaSoloActivos()
    {
        // Arrange
        _repository.Add(1, "permiso.activo", "Activo");
        _repository.Add(2, "permiso.inactivo", "Inactivo", activo: false);

        // Act
        var result = (await _handler.HandleAsync(new ListarPermisoQuery(Activo: true))).ToList();

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].IdPermiso, Is.EqualTo(1));
    }

    [Test]
    public async Task HandleAsync_ConPermisosInactivos_RetornaSoloInactivos()
    {
        // Arrange
        _repository.Add(1, "permiso.activo", "Activo");
        _repository.Add(2, "permiso.inactivo", "Inactivo", activo: false);

        // Act
        var result = (await _handler.HandleAsync(new ListarPermisoQuery(Activo: false))).ToList();

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].IdPermiso, Is.EqualTo(2));
    }
}
