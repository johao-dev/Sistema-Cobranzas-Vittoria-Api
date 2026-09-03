using Cobranzas_Vittoria.Seguridad.Application.Rol.Actualizar;
using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;
using Cobranzas_Vittoria.Tests.Unit.Seguridad.Builders;
using Cobranzas_Vittoria.Tests.Unit.Seguridad.Stubs;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cobranzas_Vittoria.Tests.Unit.Seguridad.Application;

/// <summary>
/// Pruebas unitarias de <see cref="ActualizarRolHandler"/>.
/// </summary>
public class ActualizarRolHandlerTests
{
    private StubRolRepository _repository = null!;
    private StubUsuarioActualService _usuarioActual = null!;
    private ActualizarRolHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        _repository = new StubRolRepository();
        _usuarioActual = new StubUsuarioActualService { UsuarioActual = "admin-test" };
        _handler = new ActualizarRolHandler(
            _repository,
            _usuarioActual,
            NullLogger<ActualizarRolHandler>.Instance);
    }

    [Test]
    public async Task HandleAsync_RolExistente_ActualizaNombreYDescripcion()
    {
        // Arrange
        _repository.Add(5, "Nombre original", "Desc original");
        var command = new ActualizarRolCommand(5, "Nombre nuevo", "Desc nueva", null);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.That(result.IdRol, Is.EqualTo(5));
        Assert.That(result.Nombre, Is.EqualTo("Nombre nuevo"));
        Assert.That(result.Descripcion, Is.EqualTo("Desc nueva"));
        Assert.That(result.UsuarioModificacion, Is.EqualTo("admin-test"));
        Assert.That(result.FechaModificacion, Is.Not.Null);
    }

    [Test]
    public async Task HandleAsync_ActualizacionParcialSoloNombre_DescripcionSeConserva()
    {
        // Arrange
        _repository.Add(5, "Nombre original", "Desc original");
        var command = new ActualizarRolCommand(5, "Nombre nuevo", null, null);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.That(result.Nombre, Is.EqualTo("Nombre nuevo"));
        Assert.That(result.Descripcion, Is.EqualTo("Desc original"));
    }

    [Test]
    public async Task HandleAsync_ActualizacionParcialSoloDescripcion_NombreSeConserva()
    {
        // Arrange
        _repository.Add(5, "Nombre original", "Desc original");
        var command = new ActualizarRolCommand(5, null, "Desc nueva", null);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.That(result.Nombre, Is.EqualTo("Nombre original"));
        Assert.That(result.Descripcion, Is.EqualTo("Desc nueva"));
    }

    [Test]
    public async Task HandleAsync_Desactivar_RolPasaAInactivo()
    {
        // Arrange
        _repository.Add(5, "Rol activo", "Desc");
        var command = new ActualizarRolCommand(5, null, null, false);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.That(result.Activo, Is.False);
    }

    [Test]
    public async Task HandleAsync_Activar_RolPasaAActivo()
    {
        // Arrange
        _repository.Add(5, "Rol inactivo", "Desc", activo: false);
        var command = new ActualizarRolCommand(5, null, null, true);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.That(result.Activo, Is.True);
    }

    [Test]
    public void HandleAsync_RolInexistente_LanzaValidacionNegocioSeguridadException()
    {
        var command = new ActualizarRolCommand(999, "Nombre", "Desc", null);

        var ex = Assert.ThrowsAsync<ValidacionNegocioSeguridadException>(async () =>
            await _handler.HandleAsync(command))!;

        Assert.That(ex.Errores[0].CodigoError, Is.EqualTo("ROL_NO_ENCONTRADO"));
    }

    [Test]
    public void HandleAsync_IdInvalido_LanzaValidacionNegocioSeguridadException()
    {
        var command = new ActualizarRolCommand(0, "Nombre", "Desc", null);

        Assert.ThrowsAsync<ValidacionNegocioSeguridadException>(async () =>
            await _handler.HandleAsync(command));
    }
}
