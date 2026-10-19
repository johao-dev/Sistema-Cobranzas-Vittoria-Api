using Cobranzas_Vittoria.Seguridad.Application.Permiso.Deshabilitar;
using Cobranzas_Vittoria.Seguridad.Application.Permiso.Habilitar;
using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;
using Cobranzas_Vittoria.Seguridad.Domain.Model;
using Cobranzas_Vittoria.Tests.Unit.Seguridad.Stubs;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cobranzas_Vittoria.Tests.Unit.Seguridad.Application;

public class CambiarEstadoPermisoHandlerTests
{
    private StubPermisoRepository _repository = null!;
    private EnablePermisoHandler _enableHandler = null!;
    private DisablePermisoHandler _disableHandler = null!;

    [SetUp]
    public void Setup()
    {
        _repository = new StubPermisoRepository();
        StubUsuarioActualService usuarioActual = new StubUsuarioActualService { UsuarioActual = "admin-test" };
        _enableHandler = new EnablePermisoHandler(_repository, usuarioActual, NullLogger<EnablePermisoHandler>.Instance);
        _disableHandler = new DisablePermisoHandler(_repository, usuarioActual, NullLogger<DisablePermisoHandler>.Instance);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task HandleAsync_EstadoOpuesto_PersisteCambioConAuditoria(bool habilitar)
    {
        DateTime fechaCreacion = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Permiso permiso = Permiso.Reconstruir(5, "permiso.test", "Nombre", "Descripcion", !habilitar,
            fechaCreacion, "creador");
        _repository.Permisos.Add(permiso);
        DateTime antes = DateTime.UtcNow;

        await EjecutarAsync(habilitar, permiso.IdPermiso);

        Assert.Multiple(() =>
        {
            Assert.That(permiso.Activo, Is.EqualTo(habilitar));
            Assert.That(permiso.UsuarioModificacion, Is.EqualTo("admin-test"));
            Assert.That(permiso.FechaModificacion, Is.InRange(antes, DateTime.UtcNow));
            Assert.That(permiso.Codigo, Is.EqualTo("permiso.test"));
            Assert.That(permiso.Nombre, Is.EqualTo("Nombre"));
            Assert.That(permiso.Descripcion, Is.EqualTo("Descripcion"));
            Assert.That(permiso.FechaCreacion, Is.EqualTo(fechaCreacion));
            Assert.That(permiso.UsuarioCreacion, Is.EqualTo("creador"));
            Assert.That(_repository.PermisosHabilitados, Has.Count.EqualTo(habilitar ? 1 : 0));
            Assert.That(_repository.PermisosDeshabilitados, Has.Count.EqualTo(habilitar ? 0 : 1));
        });
    }

    [TestCase(true, 0)]
    [TestCase(true, -1)]
    [TestCase(false, 0)]
    [TestCase(false, -1)]
    public void HandleAsync_IdInvalido_RechazaAntesDeConsultar(bool habilitar, int idPermiso)
    {
        ValidacionNegocioSeguridadException? excepcion = Assert.ThrowsAsync<ValidacionNegocioSeguridadException>(
            () => EjecutarAsync(habilitar, idPermiso));

        Assert.That(excepcion!.Errores.Single().CodigoError, Is.EqualTo("PERMISO_ID_INVALIDO"));
        Assert.That(_repository.IdsConsultados, Is.Empty);
        VerificarSinPersistencia();
    }

    [TestCase(true)]
    [TestCase(false)]
    public void HandleAsync_PermisoInexistente_RechazaSinPersistir(bool habilitar)
    {
        ValidacionNegocioSeguridadException? excepcion = Assert.ThrowsAsync<ValidacionNegocioSeguridadException>(
            () => EjecutarAsync(habilitar, 999));

        Assert.That(excepcion!.Errores.Single().CodigoError, Is.EqualTo("PERMISO_NO_ENCONTRADO"));
        VerificarSinPersistencia();
    }

    [TestCase(true, "PERMISO_YA_HABILITADO")]
    [TestCase(false, "PERMISO_YA_DESHABILITADO")]
    public void HandleAsync_EstadoRepetido_PropagaReglaSinPersistir(bool habilitar, string codigoError)
    {
        _repository.Add(5, "permiso.test", "Nombre", activo: habilitar);

        ValidacionNegocioSeguridadException? excepcion = Assert.ThrowsAsync<ValidacionNegocioSeguridadException>(
            () => EjecutarAsync(habilitar, 5));

        Assert.That(excepcion!.Errores.Single().CodigoError, Is.EqualTo(codigoError));
        VerificarSinPersistencia();
    }

    [TestCase(true)]
    [TestCase(false)]
    public void HandleAsync_FalloDePersistencia_PropagaExcepcion(bool habilitar)
    {
        _repository.Add(5, "permiso.test", "Nombre", activo: !habilitar);
        InvalidOperationException fallo = new InvalidOperationException("Fallo simulado de persistencia.");
        _repository.OnEnableAsync = _ => Task.FromException(fallo);
        _repository.OnDisableAsync = _ => Task.FromException(fallo);

        InvalidOperationException? excepcion = Assert.ThrowsAsync<InvalidOperationException>(
            () => EjecutarAsync(habilitar, 5));

        Assert.That(excepcion, Is.SameAs(fallo));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void HandleAsync_ComandoNulo_RechazaAntesDeConsultar(bool habilitar)
    {
        Assert.ThrowsAsync<ArgumentNullException>(() => habilitar
            ? _enableHandler.HandleAsync(null!)
            : _disableHandler.HandleAsync(null!));

        Assert.That(_repository.IdsConsultados, Is.Empty);
        VerificarSinPersistencia();
    }

    private Task EjecutarAsync(bool habilitar, int idPermiso)
        => habilitar
            ? _enableHandler.HandleAsync(new EnablePermisoCommand(idPermiso))
            : _disableHandler.HandleAsync(new DisablePermisoCommand(idPermiso));

    private void VerificarSinPersistencia()
    {
        Assert.That(_repository.PermisosHabilitados, Is.Empty);
        Assert.That(_repository.PermisosDeshabilitados, Is.Empty);
    }
}
