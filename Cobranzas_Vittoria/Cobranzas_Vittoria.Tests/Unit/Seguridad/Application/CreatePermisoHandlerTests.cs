using Cobranzas_Vittoria.Seguridad.Application.Permiso.Crear;
using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;
using Cobranzas_Vittoria.Tests.Unit.Seguridad.Builders;
using Cobranzas_Vittoria.Tests.Unit.Seguridad.Stubs;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cobranzas_Vittoria.Tests.Unit.Seguridad.Application;

/// <summary>
/// Pruebas unitarias de <see cref="CreatePermisoHandler"/>.
///
/// Se mockean (mediante stubs manuales) el repositorio y el servicio de
/// usuario actual para probar la logica del handler sin infraestructura.
/// </summary>
public class CreatePermisoHandlerTests
{
    private StubPermisoRepository _repository = null!;
    private StubUsuarioActualService _usuarioActual = null!;
    private CreatePermisoHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        _repository = new StubPermisoRepository();
        _usuarioActual = new StubUsuarioActualService { UsuarioActual = "admin-test" };
        _handler = new CreatePermisoHandler(
            _repository,
            _usuarioActual,
            NullLogger<CreatePermisoHandler>.Instance);
    }

    [Test]
    public async Task HandleAsync_DatosValidos_CreaPermisoYRetornaResultado()
    {
        // Arrange
        var command = new CreatePermisoCommand("permiso.crear", "Crear", "Permite crear");

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.That(result.IdPermiso, Is.GreaterThan(0));
        Assert.That(result.Codigo, Is.EqualTo("permiso.crear"));
        Assert.That(result.Nombre, Is.EqualTo("Crear"));
        Assert.That(result.Descripcion, Is.EqualTo("Permite crear"));
        Assert.That(result.Activo, Is.True);
        Assert.That(result.UsuarioCreacion, Is.EqualTo("admin-test"));
        Assert.That(result.FechaCreacion, Is.Not.Null);

        Assert.That(_repository.Permisos, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task HandleAsync_AplicaTrimANombreYDescripcion()
    {
        var command = new CreatePermisoCommand("permiso.trim", "  Nombre  ", "  Desc  ");

        var result = await _handler.HandleAsync(command);

        Assert.That(result.Codigo, Is.EqualTo("permiso.trim"));
        Assert.That(result.Nombre, Is.EqualTo("Nombre"));
        Assert.That(result.Descripcion, Is.EqualTo("Desc"));
    }

    [Test]
    public void HandleAsync_CodigoVacio_LanzaValidacionNegocioSeguridadException()
    {
        var command = new CreatePermisoCommand("", "Nombre", "Desc");

        Assert.ThrowsAsync<ValidacionNegocioSeguridadException>(async () =>
            await _handler.HandleAsync(command));
    }

    [Test]
    public void HandleAsync_NombreVacio_LanzaValidacionNegocioSeguridadException()
    {
        var command = new CreatePermisoCommand("permiso.test", "", "Desc");

        Assert.ThrowsAsync<ValidacionNegocioSeguridadException>(async () =>
            await _handler.HandleAsync(command));
    }

    [Test]
    public async Task HandleAsync_RepositoryAsignaId_DiferenteDeCero()
    {
        // Simula que la base de datos asigna el Id 42.
        _repository.OnAddAsync = permiso =>
            Task.FromResult(PermisoBuilder.Nuevo()
                .ConId(42)
                .ConCodigo(permiso.Codigo)
                .ConNombre(permiso.Nombre)
                .ConDescripcion(permiso.Descripcion)
                .ConAuditoriaCreacion(permiso.FechaCreacion!.Value, permiso.UsuarioCreacion!)
                .BuildDomain());

        var result = await _handler.HandleAsync(
            new CreatePermisoCommand("permiso.test", "Nombre", "Desc"));

        Assert.That(result.IdPermiso, Is.EqualTo(42));
    }

    [Test]
    public void HandleAsync_CodigoDuplicado_LanzaValidacionNegocioSeguridadException()
    {
        // Arrange
        _repository.Add(1, "permiso.duplicado", "Permiso existente");

        var command = new CreatePermisoCommand("permiso.duplicado", "Nuevo", "Desc");

        // Act & Assert
        var ex = Assert.ThrowsAsync<ValidacionNegocioSeguridadException>(async () =>
            await _handler.HandleAsync(command))!;

        Assert.That(ex.Errores[0].CodigoError, Is.EqualTo("PERMISO_CODIGO_DUPLICADO"));
        Assert.That(ex.Errores[0].Campo, Is.EqualTo(nameof(command.Codigo)));
    }
}
