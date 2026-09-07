using Cobranzas_Vittoria.Seguridad.Application.Usuario.QuitarRol;
using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;
using Cobranzas_Vittoria.Tests.Unit.Seguridad.Stubs;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cobranzas_Vittoria.Tests.Unit.Seguridad.Application;

/// <summary>
/// Pruebas unitarias de <see cref="QuitarRolHandler"/>.
/// </summary>
public class QuitarRolHandlerTests
{
    private StubUsuarioRepository _repository = null!;
    private QuitarRolHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        _repository = new StubUsuarioRepository();
        _handler = new QuitarRolHandler(
            _repository,
            NullLogger<QuitarRolHandler>.Instance);
    }

    [Test]
    public async Task HandleAsync_DatosValidos_QuitaRol()
    {
        // Arrange
        _repository.Add(1, "Juan", "Perez", "juan@local", "jperez");
        var command = new QuitarRolCommand(1, 2);

        var rolRemovido = false;
        _repository.OnQuitarRolAsync = (idUsuario, idRol) =>
        {
            rolRemovido = idUsuario == 1 && idRol == 2;
            return Task.CompletedTask;
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        Assert.That(rolRemovido, Is.True);
    }

    [Test]
    public void HandleAsync_UsuarioInexistente_LanzaValidacionNegocioSeguridadException()
    {
        var command = new QuitarRolCommand(999, 1);

        var ex = Assert.ThrowsAsync<ValidacionNegocioSeguridadException>(async () =>
            await _handler.HandleAsync(command))!;

        Assert.That(ex.Errores[0].CodigoError, Is.EqualTo("USUARIO_NO_ENCONTRADO"));
    }
}
