namespace HR28.Application.Interfaces;

/// <summary>
/// Turns an authorization code into a one-way, keyed hash for storage and lookup.
/// The plain code is never stored.
/// </summary>
public interface IAuthorizationCodeHasher
{
    /// <summary>Upper-cases and removes spaces and dashes, so "abcd-efgh" matches "ABCDEFGH".</summary>
    string Normalize(string code);

    /// <summary>Hex HMAC-SHA256 of the normalized code.</summary>
    string Hash(string code);
}
