using Microsoft.AspNetCore.Authorization;

namespace Cobranzas_Vittoria.Seguridad.Authorization;

public sealed class AuthorizePermissionAttribute : AuthorizeAttribute
{
    public AuthorizePermissionAttribute(string permission)
        => Policy = PermissionAuthorizationPolicyProvider.PolicyPrefix + permission;
}
