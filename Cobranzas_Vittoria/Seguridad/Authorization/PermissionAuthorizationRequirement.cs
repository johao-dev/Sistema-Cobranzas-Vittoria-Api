using Microsoft.AspNetCore.Authorization;

namespace Cobranzas_Vittoria.Seguridad.Authorization;

public sealed record PermissionAuthorizationRequirement(string Permission) : IAuthorizationRequirement;
