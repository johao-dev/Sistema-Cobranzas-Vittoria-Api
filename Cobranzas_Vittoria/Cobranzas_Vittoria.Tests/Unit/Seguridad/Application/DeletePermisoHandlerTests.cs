using Cobranzas_Vittoria.Seguridad.Application.Permiso.Eliminar;
using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;
using Cobranzas_Vittoria.Tests.Unit.Seguridad.Stubs;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cobranzas_Vittoria.Tests.Unit.Seguridad.Application;

/// <summary>
/// Pruebas unitarias de <see cref="DeletePermisoHandler"/>.
/// </summary>
public class DeletePermisoHandlerTests
{
    private StubPermisoRepository _repository = null!;
    private DeletePermisoHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        _repository = new StubPermisoRepository();
        _handler = new DeletePermisoHandler(
            _repository,
            NullLogger<DeletePermisoHandler>.Instance);
    }

    [Test]
    public async Task HandleAsync_PermisoExistente_EliminaDeRepositorio()
    {
        // Arrange
        _repository.Add(7, "permiso.borrar", "Borrar");
        var command = new DeletePermisoCommand(7);

        // Act
        await _handler.HandleAsync(command);

        // Assert
        Assert.That(_repository.Permisos, Has.Count.EqualTo(0));
    }

    [Test]
    public void HandleAsync_PermisoInexistente_LanzaValidacionNegocioSeguridadException()
    {
        var command = new DeletePermisoCommand(999);

        var ex = Assert.ThrowsAsync<ValidacionNegocioSeguridadException>(async () =>
            await _handler.HandleAsync(command))!;

        Assert.That(ex.Errores[0].CodigoError, Is.EqualTo("PERMISO_NO_ENCONTRADO"));
    }

    [Test]
    public void HandleAsync_IdInvalido_LanzaValidacionNegocioSeguridadException()
    {
        var command = new DeletePermisoCommand(0);

        var ex = Assert.ThrowsAsync<ValidacionNegocioSeguridadException>(async () =>
            await _handler.HandleAsync(command))!;

        Assert.That(ex.Errores[0].CodigoError, Is.EqualTo("PERMISO_ID_INVALIDO"));
    }

    [Test]
    public void HandleAsync_ComandoNulo_LanzaArgumentNullException()
    {
        Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _handler.HandleAsync(null!));
    }
}
