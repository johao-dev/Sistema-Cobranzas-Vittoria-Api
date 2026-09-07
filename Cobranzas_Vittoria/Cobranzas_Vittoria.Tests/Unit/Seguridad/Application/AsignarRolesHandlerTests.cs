using Cobranzas_Vittoria.Seguridad.Application.Usuario.AsignarRoles;
using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;
using Cobranzas_Vittoria.Tests.Unit.Seguridad.Stubs;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cobranzas_Vittoria.Tests.Unit.Seguridad.Application;

/// <summary>
/// Pruebas unitarias de <see cref="AsignarRolesHandler"/>.
/// </summary>
public class AsignarRolesHandlerTests
{
    private StubUsuarioRepository _repository = null!;
    private AsignarRolesHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        _repository = new StubUsuarioRepository();
        _handler = new AsignarRolesHandler(
            _repository,
            NullLogger<AsignarRolesHandler>.Instance);
    }

    [Test]
    public async Task HandleAsync_DatosValidos_AsignaRoles()
    {
        // Arrange
        _repository.Add(1, "Juan", "Perez", "juan@local", "jperez");
        var command = new AsignarRolesCommand(1, new[] { 1, 2 });

        var rolesAsignados = false;
        _repository.OnAsignarRolesAsync = (idUsuario, idRoles) =>
        {
            rolesAsignados = idUsuario == 1 && idRoles.SequenceEqual(new[] { 1, 2 });
            return Task.CompletedTask;
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        Assert.That(rolesAsignados, Is.True);
    }

    [Test]
    public void HandleAsync_UsuarioInexistente_LanzaValidacionNegocioSeguridadException()
    {
        var command = new AsignarRolesCommand(999, new[] { 1 });

        var ex = Assert.ThrowsAsync<ValidacionNegocioSeguridadException>(async () =>
            await _handler.HandleAsync(command))!;

        Assert.That(ex.Errores[0].CodigoError, Is.EqualTo("USUARIO_NO_ENCONTRADO"));
    }

    [Test]
    public void HandleAsync_SinRoles_LanzaValidacionNegocioSeguridadException()
    {
        // Arrange
        _repository.Add(1, "Juan", "Perez", "juan@local", "jperez");
        var command = new AsignarRolesCommand(1, Array.Empty<int>());

        // Act & Assert
        var ex = Assert.ThrowsAsync<ValidacionNegocioSeguridadException>(async () =>
            await _handler.HandleAsync(command))!;

        Assert.That(ex.Errores[0].CodigoError, Is.EqualTo("USUARIO_ROLES_REQUERIDOS"));
        Assert.That(ex.Errores[0].Campo, Is.EqualTo(nameof(command.IdRoles)));
    }
}
