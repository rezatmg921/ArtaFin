namespace ArtaFin.Application.Abstractions.Security;

/// <summary>
/// Exposes the authenticated user identity to the Application layer.
/// Implementations must derive this value from the trusted authentication context,
/// never from request-supplied command data.
/// </summary>
public interface ICurrentUser
{
    Guid UserId { get; }
}
