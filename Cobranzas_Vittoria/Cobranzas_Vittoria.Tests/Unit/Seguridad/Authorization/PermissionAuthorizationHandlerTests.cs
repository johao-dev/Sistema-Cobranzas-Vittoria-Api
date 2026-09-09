using System.Security.Claims;
using Cobranzas_Vittoria.Seguridad.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Cobranzas_Vittoria.Tests.Unit.Seguridad.Authorization;

[TestFixture]
public class PermissionAuthorizationHandlerTests
{
    [Test]
    public async Task HandleAsync_ClaimDePermisoCoincidente_SatisfaceElRequisito()
    {
        var requirement = new PermissionAuthorizationRequirement("requerimientos.ver");
        var context = CrearContexto(new Claim(PermissionAuthorizationHandler.ClaimType, "REQUERIMIENTOS.VER"), requirement);

        await new PermissionAuthorizationHandler().HandleAsync(context);

        Assert.That(context.HasSucceeded, Is.True);
    }

    [Test]
    public async Task HandleAsync_SinElPermiso_RechazaElRequisito()
    {
        var requirement = new PermissionAuthorizationRequirement("requerimientos.crear");
        var context = CrearContexto(new Claim(PermissionAuthorizationHandler.ClaimType, "requerimientos.ver"), requirement);

        await new PermissionAuthorizationHandler().HandleAsync(context);

        Assert.That(context.HasSucceeded, Is.False);
    }

    private static AuthorizationHandlerContext CrearContexto(Claim claim, IAuthorizationRequirement requirement)
        => new([requirement], new ClaimsPrincipal(new ClaimsIdentity([claim], "test")), null);
}
