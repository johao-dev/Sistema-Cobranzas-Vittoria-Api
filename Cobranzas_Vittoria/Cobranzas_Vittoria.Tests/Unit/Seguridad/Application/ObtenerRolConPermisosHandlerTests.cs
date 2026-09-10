using Cobranzas_Vittoria.Seguridad.Application.Rol.Obtener;
using Cobranzas_Vittoria.Seguridad.Application.Rol.Obtener.ConPermisos;
using Cobranzas_Vittoria.Tests.Unit.Seguridad.Builders;
using Cobranzas_Vittoria.Tests.Unit.Seguridad.Stubs;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cobranzas_Vittoria.Tests.Unit.Seguridad.Application;

public class ObtenerRolConPermisosHandlerTests
{
    private StubRolRepository _repository = null!;
    private ObtenerRolConPermisosHandler _handler = null!;

    [SetUp]
    public void Setup()
    {
        _repository = new StubRolRepository();
        _handler = new ObtenerRolConPermisosHandler(
            _repository,
            NullLogger<ObtenerRolConPermisosHandler>.Instance);
    }

    [Test]
    public async Task HandleAsync_RolExistente_MapeaRolYPermisosAsignados()
    {
        var permisoVer = PermisoBuilder.Nuevo()
            .ConId(10)
            .ConCodigo("requerimientos.ver")
            .ConNombre("Ver requerimientos")
            .BuildDomain();
        var permisoCrear = PermisoBuilder.Nuevo()
            .ConId(11)
            .ConCodigo("requerimientos.crear")
            .ConNombre("Crear requerimientos")
            .BuildDomain();
        _repository.Roles.Add(RolBuilder.Nuevo()
            .ConId(3)
            .ConNombre("Residente")
            .ConDescripcion("Rol de residente")
            .ConPermisos([permisoVer, permisoCrear])
            .BuildDomain());

        var result = await _handler.HandleAsync(new ObtenerRolQuery(3));

        Assert.That(result.IdRol, Is.EqualTo(3));
        Assert.That(result.Nombre, Is.EqualTo("Residente"));
        Assert.That(result.Permisos.Count(), Is.EqualTo(2));
        Assert.That(result.Permisos, Is.EquivalentTo(new[]
        {
            new PermisoAsignadoResult(10, "requerimientos.ver", "Ver requerimientos"),
            new PermisoAsignadoResult(11, "requerimientos.crear", "Crear requerimientos")
        }));
    }

    [Test]
    public async Task HandleAsync_RolSinPermisos_RetornaColeccionVacia()
    {
        _repository.Roles.Add(RolBuilder.Nuevo().ConId(3).BuildDomain());

        var result = await _handler.HandleAsync(new ObtenerRolQuery(3));

        Assert.That(result.Permisos, Is.Empty);
    }

    [Test]
    public void HandleAsync_RolInexistente_LanzaKeyNotFoundException()
    {
        var exception = Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            await _handler.HandleAsync(new ObtenerRolQuery(999)))!;

        Assert.That(exception.Message, Does.Contain("999"));
    }
}
