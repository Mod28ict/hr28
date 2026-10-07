namespace HR28.Domain.Entities;

/// <summary>
/// A browser a user chose to remember at sign-in ("Remember me on this device").
/// On that device the user skips typing the authorization code, but still needs the SMS
/// code. Only a keyed hash of the device key is stored; the key itself lives only in
/// the browser's HttpOnly cookie. Expires after the days set in Settings → System;
/// removed when the user's code is reset or an administrator forgets the devices.
/// </summary>
public class TrustedDevice
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    /// <summary>Hex HMAC-SHA256 of the device key (never the key itself).</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Readable label, e.g. "Chrome on Windows".</summary>
    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? LastUsedAt { get; set; }
}
