using System.Security.Cryptography;
using System.Text;
using HR28.Application.Interfaces;

namespace HR28.Infrastructure.Helpers;

/// <summary>
/// HMAC-SHA256 with a secret key held outside the database (User Secrets in
/// development, Key Vault in production). A keyed hash lets login look a code
/// up directly while a database leak alone does not reveal any codes.
/// </summary>
public class AuthorizationCodeHasher : IAuthorizationCodeHasher
{
    private readonly byte[] _key;

    public AuthorizationCodeHasher(string base64Key)
    {
        if (string.IsNullOrWhiteSpace(base64Key))
        {
            throw new InvalidOperationException(
                "Security:AuthorizationCodeKey is not configured. " +
                "Set it with 'dotnet user-secrets' (development) or Key Vault (production).");
        }

        _key = Convert.FromBase64String(base64Key);

        if (_key.Length < 32)
            throw new InvalidOperationException("Security:AuthorizationCodeKey must be at least 32 bytes.");
    }

    public string Normalize(string code) =>
        new string((code ?? string.Empty)
            .Where(c => !char.IsWhiteSpace(c) && c != '-')
            .Select(char.ToUpperInvariant)
            .ToArray());

    public string Hash(string code)
    {
        using var hmac = new HMACSHA256(_key);

        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(Normalize(code)));

        return Convert.ToHexString(hash);
    }

    public string HashOtp(Guid otpRequestId, string otp)
    {
        using var hmac = new HMACSHA256(_key);

        // The "otp|" prefix and request ID keep these hashes separate from
        // authorization code hashes and unique per code request.
        var input = $"otp|{otpRequestId:N}|{(otp ?? string.Empty).Trim()}";

        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(input)));
    }

    public string HashDeviceToken(string token)
    {
        using var hmac = new HMACSHA256(_key);

        // The "device|" prefix keeps these separate from code hashes.
        var input = $"device|{(token ?? string.Empty).Trim()}";

        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(input)));
    }
}
