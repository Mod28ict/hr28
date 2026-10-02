namespace HR28.Domain.Entities;

/// <summary>
/// An application-wide setting stored as a key/value pair.
/// Known keys and their defaults live in SystemSettingsService.
/// </summary>
public class SystemSetting
{
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; }

    public Guid? UpdatedByUserId { get; set; }
}
