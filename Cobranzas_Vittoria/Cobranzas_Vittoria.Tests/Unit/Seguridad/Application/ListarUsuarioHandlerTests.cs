using Cobranzas_Vittoria.Seguridad.Application.Usuario.Listar;
using Cobranzas_Vittoria.Tests.Unit.Seguridad.Stubs;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cobranzas_Vittoria.Tests.Unit.Seguridad.Application;

/// <summary>
/// Pruebas unitarias de <see cref="ListarUsuarioHandler"/>.
/// </summary>
public class ListarUsuarioHandlerTests
{
    private StubUsuarioRepository _repository = null!;
    private ListarUsuarioHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        _repository = new StubUsuarioRepository();
        _handler = new ListarUsuarioHandler(
            _repository,
            NullLogger<ListarUsuarioHandler>.Instance);
    }

    [Test]
    public async Task HandleAsync_SinUsuarios_RetornaListaVacia()
    {
        var result = await _handler.HandleAsync(new ListarUsuarioQuery());

        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task HandleAsync_ConUsuariosActivos_RetornaSoloActivos()
    {
        // Arrange
        _repository.Add(1, "Activo", "Usuario", "activo@local", "activo", activo: true);
        _repository.Add(2, "Inactivo", "Usuario", "inactivo@local", "inactivo", activo: false);

        // Act
        var result = (await _handler.HandleAsync(new ListarUsuarioQuery(Activo: true))).ToList();

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].IdUsuario, Is.EqualTo(1));
    }

    [Test]
    public async Task HandleAsync_ConUsuariosInactivos_RetornaSoloInactivos()
    {
        // Arrange
        _repository.Add(1, "Activo", "Usuario", "activo@local", "activo", activo: true);
        _repository.Add(2, "Inactivo", "Usuario", "inactivo@local", "inactivo", activo: false);

        // Act
        var result = (await _handler.HandleAsync(new ListarUsuarioQuery(Activo: false))).ToList();

        // Assert
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].IdUsuario, Is.EqualTo(2));
    }
}
