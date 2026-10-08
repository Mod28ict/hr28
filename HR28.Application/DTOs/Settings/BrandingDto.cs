namespace HR28.Application.DTOs.Settings;

/// <summary>
/// The client's branding, readable without signing in (the sign-in pages show it).
/// Each client deployment has its own; nothing here is built into the code.
/// </summary>
public class BrandingDto
{
    public string CampaignName { get; set; } = string.Empty;

    /// <summary>Always set: the saved short name, or initials made from the campaign name.</summary>
    public string ShortName { get; set; } = string.Empty;

    public string Tagline { get; set; } = string.Empty;

    public bool HasLogo { get; set; }

    /// <summary>Changes whenever the logo changes, so browsers fetch the new one.</summary>
    public string LogoVersion { get; set; } = string.Empty;
}
