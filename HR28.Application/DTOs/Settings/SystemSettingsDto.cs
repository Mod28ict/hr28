namespace HR28.Application.DTOs.Settings;

public class SystemSettingsDto
{
    /// <summary>The client's campaign name, e.g. "Hithaai Roohun 2028" (sidebar, sign-in pages).</summary>
    public string CampaignName { get; set; } = string.Empty;

    /// <summary>Short name / initials, e.g. "HR28" (logo mark, page titles, SMS, report files).
    /// Empty = made from the campaign name.</summary>
    public string? ShortName { get; set; }

    /// <summary>Line under the name on the sign-in pages and in the sidebar.</summary>
    public string? Tagline { get; set; }

    /// <summary>How long a login OTP stays valid.</summary>
    public int OtpExpiryMinutes { get; set; }

    /// <summary>Wrong OTP entries allowed before the code is locked.</summary>
    public int OtpMaxAttempts { get; set; }

    /// <summary>"Remember me on this device" lasts this many days; 0 turns it off.</summary>
    public int RememberDeviceDays { get; set; }

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

    /// <summary>Highest-authority role, for display.</summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>All roles; the web app combines their permissions.</summary>
    public List<string> Roles { get; set; } = new();

    /// <summary>Effective rights (roles + extra grants; everything for the Administrator).</summary>
    public List<string> Permissions { get; set; } = new();

    public bool IsAdministrator { get; set; }

    /// <summary>"Full" or "AddEncounter": what opens when this user opens a voter.</summary>
    public string VoterProfileView { get; set; } = "Full";

    /// <summary>"Dashboard" or "QuickEntry": where this user lands after signing in.</summary>
    public string StartPage { get; set; } = "Dashboard";

    /// <summary>Where voter searches run: "Areas", "All" (administrator without areas) or "None".</summary>
    public string SearchArea { get; set; } = "None";

    /// <summary>Readable scope lines, e.g. "Malé Central (all islands)".</summary>
    public List<string> Scopes { get; set; } = new();

    public DateTime? LastLoginAt { get; set; }
}
