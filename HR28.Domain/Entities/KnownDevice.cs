namespace HR28.Domain.Entities;

/// <summary>
/// A browser a user has signed in from before (owner, 2026-10-15: sign-in alerts). Signing
/// in from a browser that isn't known for the account sends the person an SMS. Only a keyed
/// hash of the browser's random key is stored; the key lives only in its HttpOnly cookie.
/// Nothing about location is recorded.
/// </summary>
public class KnownDevice
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    /// <summary>Hex HMAC-SHA256 of the browser key (never the key itself).</summary>
    public string KeyHash { get; set; } = string.Empty;

    /// <summary>Readable label, e.g. "Chrome on Android".</summary>
    public string Name { get; set; } = string.Empty;

    public DateTime FirstSeenAt { get; set; }

    public DateTime LastSeenAt { get; set; }
}
