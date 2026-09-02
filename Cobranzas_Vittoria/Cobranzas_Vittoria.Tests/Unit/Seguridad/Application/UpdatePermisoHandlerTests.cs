using Cobranzas_Vittoria.Seguridad.Application.Permiso.Actualizar;
using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;
using Cobranzas_Vittoria.Tests.Unit.Seguridad.Builders;
using Cobranzas_Vittoria.Tests.Unit.Seguridad.Stubs;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cobranzas_Vittoria.Tests.Unit.Seguridad.Application;

/// <summary>
/// Pruebas unitarias de <see cref="UpdatePermisoHandler"/>.
/// </summary>
public class UpdatePermisoHandlerTests
{
    private StubPermisoRepository _repository = null!;
    private StubUsuarioActualService _usuarioActual = null!;
    private UpdatePermisoHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        _repository = new StubPermisoRepository();
        _usuarioActual = new StubUsuarioActualService { UsuarioActual = "admin-test" };
        _handler = new UpdatePermisoHandler(
            _repository,
            _usuarioActual,
            NullLogger<UpdatePermisoHandler>.Instance);
    }

    [Test]
    public async Task HandleAsync_PermisoExistente_ActualizaNombreYDescripcion()
    {
        // Arrange
        _repository.Add(5, "permiso.no.cambia", "Nombre original", "Desc original");
        var command = new UpdatePermisoCommand(5, "Nombre nuevo", "Desc nueva");

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.That(result.IdPermiso, Is.EqualTo(5));
        Assert.That(result.Codigo, Is.EqualTo("permiso.no.cambia"));
        Assert.That(result.Nombre, Is.EqualTo("Nombre nuevo"));
        Assert.That(result.Descripcion, Is.EqualTo("Desc nueva"));
        Assert.That(result.UsuarioModificacion, Is.EqualTo("admin-test"));
        Assert.That(result.FechaModificacion, Is.Not.Null);
    }

    [Test]
    public async Task HandleAsync_ActualizacionParcialSoloNombre_DescripcionSeConserva()
    {
        // Arrange
        _repository.Add(5, "permiso.test", "Nombre original", "Desc original");
        var command = new UpdatePermisoCommand(5, "Nombre nuevo", null);

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
        _repository.Add(5, "permiso.test", "Nombre original", "Desc original");
        var command = new UpdatePermisoCommand(5, null, "Desc nueva");

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.That(result.Nombre, Is.EqualTo("Nombre original"));
        Assert.That(result.Descripcion, Is.EqualTo("Desc nueva"));
    }

    [Test]
    public void HandleAsync_PermisoInexistente_LanzaValidacionNegocioSeguridadException()
    {
        var command = new UpdatePermisoCommand(999, "Nombre", "Desc");

        var ex = Assert.ThrowsAsync<ValidacionNegocioSeguridadException>(async () =>
            await _handler.HandleAsync(command))!;

        Assert.That(ex.Errores[0].CodigoError, Is.EqualTo("PERMISO_NO_ENCONTRADO"));
    }

    [Test]
    public void HandleAsync_IdInvalido_LanzaValidacionNegocioSeguridadException()
    {
        var command = new UpdatePermisoCommand(0, "Nombre", "Desc");

        Assert.ThrowsAsync<ValidacionNegocioSeguridadException>(async () =>
            await _handler.HandleAsync(command));
    }
}
