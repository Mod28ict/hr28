using System.Security.Cryptography;

namespace HR28.Infrastructure.Helpers;

public static class AuthorizationCodeGenerator
{
    // No 0/O, 1/I/L: easy to read aloud and type.
    private const string Characters =
        "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public const int Length = 8;

    /// <summary>
    /// An 8-character code from a cryptographically secure source,
    /// formatted "ABCD-EFGH" for readability (the dash is optional when typing).
    /// </summary>
    public static string Generate()
    {
        var chars = new char[Length];

        for (var i = 0; i < Length; i++)
            chars[i] = Characters[RandomNumberGenerator.GetInt32(Characters.Length)];

        var code = new string(chars);

        return $"{code[..4]}-{code[4..]}";
    }
}
