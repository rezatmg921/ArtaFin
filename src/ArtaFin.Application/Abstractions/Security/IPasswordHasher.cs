namespace ArtaFin.Application.Abstractions.Security;

/// <summary>Hashes passwords and verifies stored password hashes.</summary>
public interface IPasswordHasher
{
    string Hash(string password);
    PasswordVerificationResult Verify(string password, string encodedHash);
}

public enum PasswordVerificationResult
{
    Failed = 0,
    Success = 1,
    SuccessRehashNeeded = 2
}
