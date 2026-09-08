using Cobranzas_Vittoria.Seguridad.Application.Rol.QuitarPermiso;
using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;
using Cobranzas_Vittoria.Tests.Unit.Seguridad.Stubs;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cobranzas_Vittoria.Tests.Unit.Seguridad.Application;

public class QuitarPermisoHandlerTests
{
    private StubRolRepository _roles = null!;
    private QuitarPermisoHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        _roles = new StubRolRepository();
        _handler = new QuitarPermisoHandler(_roles, NullLogger<QuitarPermisoHandler>.Instance);
    }

    [Test]
    public async Task HandleAsync_RolExistente_QuitaElPermisoSolicitado()
    {
        _roles.Add(1, "Administrador");
        (int idRol, int idPermiso)? recibido = null;
        _roles.OnQuitarPermisoAsync = (idRol, idPermiso) =>
        {
            recibido = (idRol, idPermiso);
            return Task.CompletedTask;
        };

        await _handler.HandleAsync(new QuitarPermisoCommand(1, 8));

        Assert.That(recibido, Is.EqualTo((1, 8)));
    }

    [Test]
    public void HandleAsync_RolInexistente_LanzaValidacionNegocioSeguridadException()
    {
        var exception = Assert.ThrowsAsync<ValidacionNegocioSeguridadException>(
            () => _handler.HandleAsync(new QuitarPermisoCommand(99, 1)))!;

        Assert.That(exception.Errores[0].CodigoError, Is.EqualTo("ROL_NO_ENCONTRADO"));
    }
}
