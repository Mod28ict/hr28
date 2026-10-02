namespace HR28.Application.DTOs.Settings;

public class SystemSettingsDto
{
    /// <summary>Shown in the sidebar under the HR28 brand.</summary>
    public string CampaignName { get; set; } = string.Empty;

    /// <summary>How long a login OTP stays valid.</summary>
    public int OtpExpiryMinutes { get; set; }

    /// <summary>Wrong OTP entries allowed before the code is locked.</summary>
    public int OtpMaxAttempts { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedByName { get; set; }
}

public class MyAccountDto
{
    public string FullName { get; set; } = string.Empty;

    public string NationalId { get; set; } = string.Empty;

    public string Designation { get; set; } = string.Empty;

    public string MobileNumber { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;

    public bool IsAdministrator { get; set; }

    /// <summary>Readable scope lines, e.g. "Malé Central (all islands)".</summary>
    public List<string> Scopes { get; set; } = new();

    public DateTime? LastLoginAt { get; set; }
}
