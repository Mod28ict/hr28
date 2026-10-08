using HR28.Application.DTOs.Settings;

namespace HR28.Application.Interfaces;

public interface ISystemSettingsService
{
    /// <summary>Current settings, with defaults for anything never saved.</summary>
    Task<SystemSettingsDto> GetAsync();

    /// <summary>Validates, saves and audits each changed value.</summary>
    Task<SystemSettingsDto> UpdateAsync(SystemSettingsDto settings, Guid userId);

    Task<MyAccountDto?> GetMyAccountAsync(Guid userId);

    /// <summary>The client's name, short name, tagline and whether there is a logo.</summary>
    Task<BrandingDto> GetBrandingAsync();

    /// <summary>The logo image, or null when none was uploaded.</summary>
    Task<(byte[] Content, string ContentType)?> GetLogoAsync();

    /// <summary>Checks, cleans, saves and audits a new logo; returns a plain-language error or null.</summary>
    Task<string?> SaveLogoAsync(byte[] data, Guid userId);

    Task RemoveLogoAsync(Guid userId);
}
