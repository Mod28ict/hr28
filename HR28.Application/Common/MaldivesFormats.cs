using System.Text.RegularExpressions;

namespace HR28.Application.Common;

/// <summary>
/// Shared format rules for voters, influencers and users:
/// National ID = one letter + 6 digits (e.g. A123456, 7 characters);
/// mobile / contact number = exactly 7 digits.
/// The web forms enforce the same rules (maxlength 7, digits-only mobile field).
/// </summary>
public static class MaldivesFormats
{
    private static readonly Regex NationalIdPattern = new(@"^[A-Z]\d{6}$");
    private static readonly Regex MobilePattern = new(@"^\d{7}$");

    public const string NationalIdMessage = "National ID must be one letter followed by 6 digits, e.g. A123456.";
    public const string MobileMessage = "Mobile number must be exactly 7 digits, e.g. 7771234.";

    /// <summary>Trimmed and upper-cased.</summary>
    public static string CleanNationalId(string? value) =>
        (value ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>Removes spaces and dashes people type in phone numbers.</summary>
    public static string CleanMobile(string? value) =>
        new string((value ?? string.Empty).Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray());

    public static bool IsNationalId(string value) => NationalIdPattern.IsMatch(value);

    public static bool IsMobile(string value) => MobilePattern.IsMatch(value);

    public static void RequireNationalId(string nationalId)
    {
        if (!IsNationalId(nationalId))
            throw new BusinessRuleException(NationalIdMessage);
    }

    /// <summary>
    /// Checks a mobile number; empty is allowed unless <paramref name="required"/>.
    /// <paramref name="label"/> is how the field is named on the form, e.g. "Contact number".
    /// </summary>
    public static void RequireMobile(string mobile, string label = "Mobile number", bool required = false)
    {
        if (mobile.Length == 0)
        {
            if (required)
                throw new BusinessRuleException($"{label} is required (7 digits).");
            return;
        }

        if (!IsMobile(mobile))
            throw new BusinessRuleException($"{label} must be exactly 7 digits, e.g. 7771234.");
    }
}
