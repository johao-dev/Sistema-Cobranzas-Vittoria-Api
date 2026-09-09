using Microsoft.AspNetCore.Authorization;

namespace Cobranzas_Vittoria.Seguridad.Authorization;

public sealed class PermissionAuthorizationHandler
    : AuthorizationHandler<PermissionAuthorizationRequirement>
{
    public const string ClaimType = "permission";

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionAuthorizationRequirement requirement)
    {
        if (context.User.HasClaim(
                claim => claim.Type == ClaimType &&
                         string.Equals(claim.Value, requirement.Permission, StringComparison.OrdinalIgnoreCase)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
