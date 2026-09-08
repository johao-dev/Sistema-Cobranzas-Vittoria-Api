using System.Security.Claims;
using Cobranzas_Vittoria.Seguridad.Infrastructure.Services;
using Microsoft.AspNetCore.Http;

namespace Cobranzas_Vittoria.Tests.Unit.Seguridad.Infrastructure;

[TestFixture]
public class UsuarioActualServiceTests
{
    [Test]
    public void UsuarioAutenticado_ResuelveLosClaimsDisponibles()
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "42"),
            new Claim(ClaimTypes.Name, "ana"),
            new Claim(ClaimTypes.Email, "ana@vittoria.local")
        ], "Test"));
        var service = CrearServicio(context);

        Assert.Multiple(() =>
        {
            Assert.That(service.EstaAutenticado, Is.True);
            Assert.That(service.IdUsuario, Is.EqualTo(42));
            Assert.That(service.UsuarioLogin, Is.EqualTo("ana"));
            Assert.That(service.Correo, Is.EqualTo("ana@vittoria.local"));
            Assert.That(service.ObtenerUsuarioActual(), Is.EqualTo("ana"));
        });
    }

    [Test]
    public void UsuarioAnonimo_NoExponeDatosYConservaAuditoriaSistema()
    {
        var service = CrearServicio(new DefaultHttpContext());

        Assert.Multiple(() =>
        {
            Assert.That(service.EstaAutenticado, Is.False);
            Assert.That(service.IdUsuario, Is.Null);
            Assert.That(service.UsuarioLogin, Is.Null);
            Assert.That(service.Correo, Is.Null);
            Assert.That(service.ObtenerUsuarioActual(), Is.EqualTo("sistema"));
        });
    }

    [Test]
    public void ClaimDeIdInvalido_NoSeConvierteEnUnIdDeUsuario()
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "no-es-entero")], "Test"));
        var service = CrearServicio(context);

        Assert.That(service.IdUsuario, Is.Null);
    }

    private static UsuarioActualService CrearServicio(HttpContext? context) =>
        new(new HttpContextAccessor { HttpContext = context });
}
