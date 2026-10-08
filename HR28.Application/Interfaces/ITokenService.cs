using HR28.Domain.Entities;

namespace HR28.Application.Interfaces;

public interface ITokenService
{
    /// <summary>Claim holding when the person signed in (Unix seconds); kept across refreshes.</summary>
    const string SignedInAtClaim = "auth_time";

    /// <summary>
    /// A 1-hour token. <paramref name="signedInAtUtc"/> is the original sign-in time
    /// (now for a new sign-in; carried over when an active session is refreshed).
    /// </summary>
    Task<string> GenerateTokenAsync(User user, DateTime? signedInAtUtc = null);
}