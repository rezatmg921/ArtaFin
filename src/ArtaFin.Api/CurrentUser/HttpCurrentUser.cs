using System.Security.Claims;
using ArtaFin.Application.Abstractions.Security;

namespace ArtaFin.Api.CurrentUser;

internal sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor)
    : ICurrentUser
{
    public Guid UserId
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            var value = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? user?.FindFirst("sub")?.Value;

            return Guid.TryParse(value, out var userId) ? userId : Guid.Empty;
        }
    }
}
