using Microsoft.AspNetCore.Authorization;

namespace ArtaFin.Api.Security;

internal sealed record PermissionRequirement(string PermissionCode) : IAuthorizationRequirement;
