using System.Security.Cryptography;

namespace HR28.Infrastructure.Helpers;

public static class OtpGenerator
{
    public const int Length = 6;

    /// <summary>
    /// A 6-digit code from a cryptographically secure random source
    /// (000000–999999, leading zeros kept).
    /// </summary>
    public static string Generate()
    {
        return RandomNumberGenerator
            .GetInt32(0, 1_000_000)
            .ToString("D6");
    }
}
