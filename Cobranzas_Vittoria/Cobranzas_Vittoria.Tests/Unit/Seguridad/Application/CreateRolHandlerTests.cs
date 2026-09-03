using Cobranzas_Vittoria.Seguridad.Application.Rol.Crear;
using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;
using Cobranzas_Vittoria.Tests.Unit.Seguridad.Builders;
using Cobranzas_Vittoria.Tests.Unit.Seguridad.Stubs;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cobranzas_Vittoria.Tests.Unit.Seguridad.Application;

/// <summary>
/// Pruebas unitarias de <see cref="CreateRolHandler"/>.
/// </summary>
public class CreateRolHandlerTests
{
    private StubRolRepository _repository = null!;
    private StubUsuarioActualService _usuarioActual = null!;
    private CreateRolHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        _repository = new StubRolRepository();
        _usuarioActual = new StubUsuarioActualService { UsuarioActual = "admin-test" };
        _handler = new CreateRolHandler(
            _repository,
            _usuarioActual,
            NullLogger<CreateRolHandler>.Instance);
    }

    [Test]
    public async Task HandleAsync_DatosValidos_CreaRolYRetornaResultado()
    {
        // Arrange
        var command = new CreateRolCommand("Rol nuevo", "Descripcion");

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.That(result.IdRol, Is.GreaterThan(0));
        Assert.That(result.Nombre, Is.EqualTo("Rol nuevo"));
        Assert.That(result.Descripcion, Is.EqualTo("Descripcion"));
        Assert.That(result.Activo, Is.True);
        Assert.That(result.UsuarioCreacion, Is.EqualTo("admin-test"));
        Assert.That(result.FechaCreacion, Is.Not.Null);

        Assert.That(_repository.Roles, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task HandleAsync_AplicaTrimANombreYDescripcion()
    {
        var command = new CreateRolCommand("  Nombre  ", "  Desc  ");

        var result = await _handler.HandleAsync(command);

        Assert.That(result.Nombre, Is.EqualTo("Nombre"));
        Assert.That(result.Descripcion, Is.EqualTo("Desc"));
    }

    [Test]
    public void HandleAsync_NombreVacio_LanzaValidacionNegocioSeguridadException()
    {
        var command = new CreateRolCommand("", "Desc");

        Assert.ThrowsAsync<ValidacionNegocioSeguridadException>(async () =>
            await _handler.HandleAsync(command));
    }

    [Test]
    public void HandleAsync_NombreDuplicado_LanzaValidacionNegocioSeguridadException()
    {
        // Arrange
        _repository.Add(1, "Rol duplicado", "Existente");
        var command = new CreateRolCommand("Rol duplicado", "Nuevo");

        // Act & Assert
        var ex = Assert.ThrowsAsync<ValidacionNegocioSeguridadException>(async () =>
            await _handler.HandleAsync(command))!;

        Assert.That(ex.Errores[0].CodigoError, Is.EqualTo("ROL_NOMBRE_DUPLICADO"));
    }

    [Test]
    public void HandleAsync_NombreDuplicadoConDiferenteCasing_LanzaValidacionNegocioSeguridadException()
    {
        // Arrange
        _repository.Add(1, "Rol Duplicado", "Existente");
        var command = new CreateRolCommand("rol duplicado", "Nuevo");

        // Act & Assert
        Assert.ThrowsAsync<ValidacionNegocioSeguridadException>(async () =>
            await _handler.HandleAsync(command));
    }
}
