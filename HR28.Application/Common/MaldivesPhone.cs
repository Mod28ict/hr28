namespace HR28.Application.Common;

/// <summary>
/// Maldivian mobile numbers: 7 digits starting with 7 (Dhiraagu) or 9 (Ooredoo), with or
/// without the country code 960. SMS providers want the international form "960XXXXXXX".
/// </summary>
public static class MaldivesPhone
{
    /// <summary>"7771234", "960 777 1234" or "+9607771234" → "9607771234"; null if it isn't a Maldivian mobile number.</summary>
    public static string? ToInternational(string? number)
    {
        var digits = new string((number ?? string.Empty).Where(char.IsDigit).ToArray());

        if (digits.Length == 10 && digits.StartsWith("960", StringComparison.Ordinal))
            digits = digits[3..];

        return digits.Length == 7 && (digits[0] == '7' || digits[0] == '9')
            ? "960" + digits
            : null;
    }
}
