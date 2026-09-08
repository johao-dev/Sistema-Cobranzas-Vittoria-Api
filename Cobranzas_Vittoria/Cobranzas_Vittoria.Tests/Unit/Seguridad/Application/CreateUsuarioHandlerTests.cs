using Cobranzas_Vittoria.Seguridad.Application.Usuario.Crear;
using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;
using Cobranzas_Vittoria.Tests.Unit.Seguridad.Builders;
using Cobranzas_Vittoria.Tests.Unit.Seguridad.Stubs;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cobranzas_Vittoria.Tests.Unit.Seguridad.Application;

/// <summary>
/// Pruebas unitarias de <see cref="CreateUsuarioHandler"/>.
///
/// Se mockean (mediante stubs manuales) el repositorio y el servicio de
/// usuario actual para probar la logica del handler sin infraestructura.
/// </summary>
public class CreateUsuarioHandlerTests
{
    private StubUsuarioRepository _repository = null!;
    private StubUsuarioActualService _usuarioActual = null!;
    private StubPasswordHasher _passwordHasher = null!;
    private CreateUsuarioHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        _repository = new StubUsuarioRepository();
        _usuarioActual = new StubUsuarioActualService { UsuarioActual = "admin-test" };
        _passwordHasher = new StubPasswordHasher();
        
        _handler = new CreateUsuarioHandler(
            _repository,
            _usuarioActual,
            _passwordHasher,
            NullLogger<CreateUsuarioHandler>.Instance);
    }

    [Test]
    public async Task HandleAsync_DatosValidos_CreaUsuarioYRetornaResultado()
    {
        // Arrange
        var command = new CreateUsuarioCommand(
            "Juan",
            "Perez",
            "juan.perez@local",
            "jperez",
            "password");

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.That(result.IdUsuario, Is.GreaterThan(0));
        Assert.That(result.Nombres, Is.EqualTo("Juan"));
        Assert.That(result.Apellidos, Is.EqualTo("Perez"));
        Assert.That(result.Correo, Is.EqualTo("juan.perez@local"));
        Assert.That(result.UsuarioLogin, Is.EqualTo("jperez"));
        Assert.That(result.Activo, Is.True);
        Assert.That(result.UsuarioCreacion, Is.EqualTo("admin-test"));
        Assert.That(result.FechaCreacion, Is.Not.Null);

        Assert.That(_repository.Usuarios, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task HandleAsync_AplicaTrimANombreApellidoLogin()
    {
        var command = new CreateUsuarioCommand(
            "  Juan  ",
            "  Perez  ",
            "juan.perez@local",
            "  jperez  ",
            "password");

        var result = await _handler.HandleAsync(command);

        Assert.That(result.Nombres, Is.EqualTo("Juan"));
        Assert.That(result.Apellidos, Is.EqualTo("Perez"));
        Assert.That(result.Correo, Is.EqualTo("juan.perez@local"));
        Assert.That(result.UsuarioLogin, Is.EqualTo("jperez"));
    }

    [Test]
    public void HandleAsync_CorreoDuplicado_LanzaValidacionNegocioSeguridadException()
    {
        // Arrange
        _repository.Add(
            1,
            "Existente",
            "Usuario",
            "juan.perez@local",
            "existente",
            "password");

        var command = new CreateUsuarioCommand(
            "Juan",
            "Perez",
            "juan.perez@local",
            "jperez",
            "password");

        // Act & Assert
        var ex = Assert.ThrowsAsync<ValidacionNegocioSeguridadException>(async () =>
            await _handler.HandleAsync(command))!;

        Assert.That(ex.Errores[0].CodigoError, Is.EqualTo("USUARIO_CORREO_DUPLICADO"));
        Assert.That(ex.Errores[0].Campo, Is.EqualTo(nameof(command.Correo)));
    }

    [Test]
    public void HandleAsync_CorreoInvalido_LanzaArgumentException()
    {
        var command = new CreateUsuarioCommand(
            "Juan",
            "Perez",
            "no-es-un-correo",
            "jperez",
            "password");

        Assert.ThrowsAsync<ArgumentException>(async () =>
            await _handler.HandleAsync(command));
    }

    [Test]
    public void HandleAsync_CamposRequeridosVacios_LanzaValidacionNegocioSeguridadException()
    {
        var command = new CreateUsuarioCommand("", "", "", "", "");

        Assert.ThrowsAsync<ValidacionNegocioSeguridadException>(async () =>
            await _handler.HandleAsync(command));
    }

    [Test]
    public async Task HandleAsync_RepositoryAsignaId_DiferenteDeCero()
    {
        // Simula que la base de datos asigna el Id 99.
        _repository.OnAddAsync = usuario =>
            Task.FromResult(UsuarioBuilder.Nuevo()
                .ConId(99)
                .ConNombres(usuario.Nombres)
                .ConApellidos(usuario.Apellidos)
                .ConCorreo(usuario.Correo.Value)
                .ConUsuarioLogin(usuario.UsuarioLogin)
                .ConPasswordHash(usuario.PasswordHash)
                .BuildDomain());

        var result = await _handler.HandleAsync(
            new CreateUsuarioCommand(
                "Juan",
                "Perez",
                "juan.perez@local",
                "jperez",
                "password"));

        Assert.That(result.IdUsuario, Is.EqualTo(99));
    }
}
