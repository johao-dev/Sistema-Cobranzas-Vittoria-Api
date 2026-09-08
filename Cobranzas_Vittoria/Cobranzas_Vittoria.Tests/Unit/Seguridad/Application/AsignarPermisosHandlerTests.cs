using Cobranzas_Vittoria.Seguridad.Application.Rol.AsignarPermisos;
using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;
using Cobranzas_Vittoria.Tests.Unit.Seguridad.Stubs;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cobranzas_Vittoria.Tests.Unit.Seguridad.Application;

public class AsignarPermisosHandlerTests
{
    private StubRolRepository _roles = null!;
    private StubUsuarioActualService _usuarioActual = null!;
    private AsignarPermisosHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        _roles = new StubRolRepository();
        _usuarioActual = new StubUsuarioActualService { UsuarioActual = "admin-test" };
        _handler = new AsignarPermisosHandler(
            _roles,
            _usuarioActual,
            NullLogger<AsignarPermisosHandler>.Instance);
    }

    [Test]
    public async Task HandleAsync_DatosValidos_AsignaPermisosSinDuplicados()
    {
        _roles.Add(1, "Administrador");
        (int idRol, int[] permisos, string usuario)? recibido = null;
        _roles.OnAsignarPermisosAsync = (idRol, permisos, usuario) =>
        {
            recibido = (idRol, permisos.ToArray(), usuario);
            return Task.CompletedTask;
        };

        await _handler.HandleAsync(new AsignarPermisosCommand(1, new[] { 3, 4, 3 }));

        Assert.That(recibido, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(recibido!.Value.idRol, Is.EqualTo(1));
            Assert.That(recibido.Value.permisos, Is.EqualTo(new[] { 3, 4 }));
            Assert.That(recibido.Value.usuario, Is.EqualTo("admin-test"));
        });
    }

    [Test]
    public void HandleAsync_RolInexistente_LanzaValidacionNegocioSeguridadException()
    {
        var exception = Assert.ThrowsAsync<ValidacionNegocioSeguridadException>(
            () => _handler.HandleAsync(new AsignarPermisosCommand(99, new[] { 1 })))!;

        Assert.That(exception.Errores[0].CodigoError, Is.EqualTo("ROL_NO_ENCONTRADO"));
    }

    [Test]
    public void HandleAsync_SinPermisos_LanzaValidacionNegocioSeguridadException()
    {
        _roles.Add(1, "Administrador");

        var exception = Assert.ThrowsAsync<ValidacionNegocioSeguridadException>(
            () => _handler.HandleAsync(new AsignarPermisosCommand(1, Array.Empty<int>())))!;

        Assert.Multiple(() =>
        {
            Assert.That(exception.Errores[0].CodigoError, Is.EqualTo("ROL_PERMISOS_REQUERIDOS"));
            Assert.That(exception.Errores[0].Campo, Is.EqualTo("IdPermisos"));
        });
    }
}
