using HR28.Application.DTOs.Settings;

namespace HR28.Application.Interfaces;

public interface ISystemSettingsService
{
    /// <summary>Current settings, with defaults for anything never saved.</summary>
    Task<SystemSettingsDto> GetAsync();

    /// <summary>Validates, saves and audits each changed value.</summary>
    Task<SystemSettingsDto> UpdateAsync(SystemSettingsDto settings, Guid userId);

    Task<MyAccountDto?> GetMyAccountAsync(Guid userId);
}
