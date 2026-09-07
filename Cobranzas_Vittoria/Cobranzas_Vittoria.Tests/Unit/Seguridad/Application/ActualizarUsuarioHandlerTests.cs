using Cobranzas_Vittoria.Seguridad.Application.Usuario.Actualizar;
using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;
using Cobranzas_Vittoria.Tests.Unit.Seguridad.Stubs;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cobranzas_Vittoria.Tests.Unit.Seguridad.Application;

/// <summary>
/// Pruebas unitarias de <see cref="ActualizarUsuarioHandler"/>.
/// </summary>
public class ActualizarUsuarioHandlerTests
{
    private StubUsuarioRepository _repository = null!;
    private ActualizarUsuarioHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        _repository = new StubUsuarioRepository();
        _handler = new ActualizarUsuarioHandler(
            _repository,
            NullLogger<ActualizarUsuarioHandler>.Instance);
    }

    [Test]
    public async Task HandleAsync_UsuarioExistente_ActualizaNombreYApellidos()
    {
        // Arrange
        _repository.Add(5, "Juan", "Perez", "juan@local", "jperez");
        var command = new ActualizarUsuarioCommand(
            5,
            "Carlos",
            "Lopez",
            null,
            null,
            null,
            null);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.That(result.IdUsuario, Is.EqualTo(5));
        Assert.That(result.Nombres, Is.EqualTo("Carlos"));
        Assert.That(result.Apellidos, Is.EqualTo("Lopez"));
        Assert.That(result.Correo, Is.EqualTo("juan@local"));
        Assert.That(result.UsuarioLogin, Is.EqualTo("jperez"));
    }

    [Test]
    public async Task HandleAsync_ActualizacionParcialSoloNombre_ApellidoSeConserva()
    {
        // Arrange
        _repository.Add(5, "Juan", "Perez", "juan@local", "jperez");
        var command = new ActualizarUsuarioCommand(
            5,
            "Carlos",
            null,
            null,
            null,
            null,
            null);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.That(result.Nombres, Is.EqualTo("Carlos"));
        Assert.That(result.Apellidos, Is.EqualTo("Perez"));
    }

    [Test]
    public async Task HandleAsync_ActualizacionDeCorreo_CorreoActualizado()
    {
        // Arrange
        _repository.Add(5, "Juan", "Perez", "juan@local", "jperez");
        var command = new ActualizarUsuarioCommand(
            5,
            null,
            null,
            "nuevo@local",
            null,
            null,
            null);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.That(result.Correo, Is.EqualTo("nuevo@local"));
    }

    [Test]
    public void HandleAsync_CorreoDuplicado_LanzaValidacionNegocioSeguridadException()
    {
        // Arrange
        _repository.Add(5, "Juan", "Perez", "juan@local", "jperez");
        _repository.Add(7, "Otro", "Usuario", "duplicado@local", "otro");

        var command = new ActualizarUsuarioCommand(
            5,
            null,
            null,
            "duplicado@local",
            null,
            null,
            null);

        // Act & Assert
        var ex = Assert.ThrowsAsync<ValidacionNegocioSeguridadException>(async () =>
            await _handler.HandleAsync(command))!;

        Assert.That(ex.Errores[0].CodigoError, Is.EqualTo("USUARIO_CORREO_DUPLICADO"));
        Assert.That(ex.Errores[0].Campo, Is.EqualTo(nameof(command.Correo)));
    }

    [Test]
    public async Task HandleAsync_ActualizacionDeLoginYPassword_CredencialesActualizadas()
    {
        // Arrange
        _repository.Add(5, "Juan", "Perez", "juan@local", "jperez");
        var command = new ActualizarUsuarioCommand(
            5,
            null,
            null,
            null,
            "nuevo.login",
            "nuevo-hash",
            null);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.That(result.UsuarioLogin, Is.EqualTo("nuevo.login"));
    }

    [Test]
    public async Task HandleAsync_DesactivarUsuario_ActivoEsFalso()
    {
        // Arrange
        _repository.Add(5, "Juan", "Perez", "juan@local", "jperez");
        var command = new ActualizarUsuarioCommand(
            5,
            null,
            null,
            null,
            null,
            null,
            false);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.That(result.Activo, Is.False);
    }

    [Test]
    public void HandleAsync_UsuarioInexistente_LanzaValidacionNegocioSeguridadException()
    {
        var command = new ActualizarUsuarioCommand(
            999,
            "Carlos",
            "Lopez",
            null,
            null,
            null,
            null);

        var ex = Assert.ThrowsAsync<ValidacionNegocioSeguridadException>(async () =>
            await _handler.HandleAsync(command))!;

        Assert.That(ex.Errores[0].CodigoError, Is.EqualTo("USUARIO_NO_ENCONTRADO"));
    }

    [Test]
    public void HandleAsync_IdInvalido_LanzaValidacionNegocioSeguridadException()
    {
        var command = new ActualizarUsuarioCommand(
            0,
            "Carlos",
            "Lopez",
            null,
            null,
            null,
            null);

        Assert.ThrowsAsync<ValidacionNegocioSeguridadException>(async () =>
            await _handler.HandleAsync(command));
    }
}
