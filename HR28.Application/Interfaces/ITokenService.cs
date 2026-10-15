using HR28.Domain.Entities;

namespace HR28.Application.Interfaces;

public interface ITokenService
{
    /// <summary>Claim holding when the person signed in (Unix seconds); kept across refreshes.</summary>
    const string SignedInAtClaim = "auth_time";

    /// <summary>
    /// A 1-hour token. <paramref name="signedInAtUtc"/> is the original sign-in time
    /// (now for a new sign-in; carried over when an active session is refreshed).
    /// <paramref name="steppedUpAtUtc"/> is when the person last confirmed it's them
    /// (step-up); carried over on refresh, checked by the API for sensitive actions.
    /// </summary>
    Task<string> GenerateTokenAsync(User user, DateTime? signedInAtUtc = null, DateTime? steppedUpAtUtc = null);
}