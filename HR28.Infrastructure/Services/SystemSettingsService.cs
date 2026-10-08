using HR28.Application.Common;
using HR28.Application.DTOs.Settings;
using HR28.Application.Interfaces;
using HR28.Domain.Entities;
using HR28.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HR28.Infrastructure.Services;

public class SystemSettingsService : ISystemSettingsService
{
    public const string CampaignNameKey = "CampaignName";
    public const string OtpExpiryMinutesKey = "OtpExpiryMinutes";
    public const string OtpMaxAttemptsKey = "OtpMaxAttempts";
    public const string RememberDeviceDaysKey = "RememberDeviceDays";
    public const string ShortNameKey = "ShortName";
    public const string TaglineKey = "Tagline";

    public const string DefaultTagline = "Campaign Intelligence Platform";

    /// <summary>Largest logo accepted.</summary>
    public const int MaxLogoBytes = 1024 * 1024;

    /// <summary>
    /// Initials made from the campaign name when no short name was saved: the first
    /// letter of each word, and the last two digits of a number ("Hithaai Roohun 2028" →
    /// "HR28", "Campaign Intelligence" → "CI").
    /// </summary>
    public static string InitialsOf(string campaignName)
    {
        var parts = (campaignName ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.All(char.IsDigit) ? (w.Length > 2 ? w[^2..] : w) : char.ToUpperInvariant(w[0]).ToString())
            .Where(p => p.Length > 0 && p.All(char.IsLetterOrDigit))
            .Take(4);

        var initials = string.Concat(parts);
        return initials.Length > 0 ? initials : "HQ";
    }

    // Defaults apply until an administrator saves a value.
    public const string DefaultCampaignName = "Campaign Intelligence";
    public const int DefaultOtpExpiryMinutes = 5;
    public const int DefaultOtpMaxAttempts = 5;
    public const int DefaultRememberDeviceDays = 30;

    private readonly HR28DbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly IAccessScopeService _accessScopeService;

    public SystemSettingsService(
        HR28DbContext dbContext,
        IAuditService auditService,
        IAccessScopeService accessScopeService)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _accessScopeService = accessScopeService;
    }

    public async Task<SystemSettingsDto> GetAsync()
    {
        var stored = await _dbContext.SystemSettings
            .AsNoTracking()
            .ToListAsync();

        string Text(string key, string fallback) =>
            stored.FirstOrDefault(s => s.Key == key)?.Value is { Length: > 0 } value
                ? value
                : fallback;

        int Number(string key, int fallback) =>
            int.TryParse(stored.FirstOrDefault(s => s.Key == key)?.Value, out var value)
                ? value
                : fallback;

        var latest = stored.OrderByDescending(s => s.UpdatedAt).FirstOrDefault();

        string? updatedByName = null;

        if (latest?.UpdatedByUserId is Guid updatedBy)
        {
            updatedByName = await _dbContext.Users
                .Where(u => u.Id == updatedBy)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync();
        }

        return new SystemSettingsDto
        {
            CampaignName = Text(CampaignNameKey, DefaultCampaignName),
            OtpExpiryMinutes = Number(OtpExpiryMinutesKey, DefaultOtpExpiryMinutes),
            OtpMaxAttempts = Number(OtpMaxAttemptsKey, DefaultOtpMaxAttempts),
            RememberDeviceDays = Number(RememberDeviceDaysKey, DefaultRememberDeviceDays),
            ShortName = stored.FirstOrDefault(x => x.Key == ShortNameKey)?.Value ?? string.Empty,
            Tagline = stored.FirstOrDefault(x => x.Key == TaglineKey)?.Value ?? string.Empty,
            UpdatedAt = latest?.UpdatedAt,
            UpdatedByName = updatedByName
        };
    }

    public async Task<SystemSettingsDto> UpdateAsync(SystemSettingsDto settings, Guid userId)
    {
        var campaignName = settings.CampaignName?.Trim() ?? string.Empty;

        if (campaignName.Length is < 2 or > 80)
            throw new BusinessRuleException("Campaign name must be between 2 and 80 characters.");

        if (settings.OtpExpiryMinutes is < 1 or > 15)
            throw new BusinessRuleException("OTP expiry must be between 1 and 15 minutes.");

        if (settings.OtpMaxAttempts is < 3 or > 10)
            throw new BusinessRuleException("Maximum OTP attempts must be between 3 and 10.");

        var shortName = (settings.ShortName ?? string.Empty).Trim();

        if (shortName.Length > 8 || !shortName.All(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-'))
            throw new BusinessRuleException("Short name can have up to 8 letters, numbers, spaces or dashes (e.g. FT28). Leave it empty to use the campaign name's initials.");

        var tagline = (settings.Tagline ?? string.Empty).Trim();

        if (tagline.Length > 80)
            throw new BusinessRuleException("Tagline can be up to 80 characters.");

        if (settings.RememberDeviceDays is < 0 or > 90)
            throw new BusinessRuleException("Remember devices for must be between 0 (off) and 90 days.");

        var current = await GetAsync();

        // Each changed value is saved and audited with its old and new value.
        await SaveIfChangedAsync(CampaignNameKey, "Campaign name",
            current.CampaignName, campaignName, userId);

        await SaveIfChangedAsync(OtpExpiryMinutesKey, "OTP expiry (minutes)",
            current.OtpExpiryMinutes.ToString(), settings.OtpExpiryMinutes.ToString(), userId);

        await SaveIfChangedAsync(OtpMaxAttemptsKey, "Maximum OTP attempts",
            current.OtpMaxAttempts.ToString(), settings.OtpMaxAttempts.ToString(), userId);

        await SaveIfChangedAsync(ShortNameKey, "Short name",
            current.ShortName ?? string.Empty, shortName, userId);

        await SaveIfChangedAsync(TaglineKey, "Tagline",
            current.Tagline ?? string.Empty, tagline, userId);

        await SaveIfChangedAsync(RememberDeviceDaysKey, "Remember devices for (days)",
            current.RememberDeviceDays.ToString(), settings.RememberDeviceDays.ToString(), userId);

        return await GetAsync();
    }

    public async Task<BrandingDto> GetBrandingAsync()
    {
        var settings = await GetAsync();

        var logo = await _dbContext.BrandLogos
            .AsNoTracking()
            .Select(l => new { l.UpdatedAt })
            .FirstOrDefaultAsync();

        return new BrandingDto
        {
            CampaignName = settings.CampaignName,
            ShortName = string.IsNullOrWhiteSpace(settings.ShortName) ? InitialsOf(settings.CampaignName) : settings.ShortName,
            Tagline = string.IsNullOrWhiteSpace(settings.Tagline) ? DefaultTagline : settings.Tagline,
            HasLogo = logo != null,
            LogoVersion = logo?.UpdatedAt.Ticks.ToString() ?? string.Empty
        };
    }

    public async Task<(byte[] Content, string ContentType)?> GetLogoAsync()
    {
        var logo = await _dbContext.BrandLogos.AsNoTracking().FirstOrDefaultAsync();

        return logo == null ? null : (logo.Content, logo.ContentType);
    }

    public async Task<string?> SaveLogoAsync(byte[] data, Guid userId)
    {
        if (data.Length == 0)
            return "Please choose a logo first.";

        if (data.Length > MaxLogoBytes)
            return "The logo is too large. Please choose one under 1 MB.";

        // Same checks as voter photos: really a JPEG/PNG, rebuilt without hidden data.
        var (content, contentType, error) = HR28.Infrastructure.Helpers.PhotoSanitizer.Clean(data);

        if (error != null || content == null || contentType == null)
            return (error ?? "That logo could not be read.").Replace("photo", "logo");

        var logo = await _dbContext.BrandLogos.FindAsync(1);
        var replacing = logo != null;

        if (logo == null)
        {
            logo = new BrandLogo { Id = 1 };
            _dbContext.BrandLogos.Add(logo);
        }

        logo.Content = content;
        logo.ContentType = contentType;
        logo.UpdatedAt = DateTime.UtcNow;
        logo.UpdatedByUserId = userId;

        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(userId, replacing ? "Logo replaced" : "Logo added", "SystemSetting", "Logo");

        return null;
    }

    public async Task RemoveLogoAsync(Guid userId)
    {
        var logo = await _dbContext.BrandLogos.FindAsync(1);

        if (logo == null)
            return;

        _dbContext.BrandLogos.Remove(logo);
        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(userId, "Logo removed", "SystemSetting", "Logo");
    }

    private async Task SaveIfChangedAsync(
        string key,
        string label,
        string oldValue,
        string newValue,
        Guid userId)
    {
        if (oldValue == newValue)
            return;

        var setting = await _dbContext.SystemSettings.FindAsync(key);

        if (setting == null)
        {
            setting = new SystemSetting { Key = key };
            _dbContext.SystemSettings.Add(setting);
        }

        setting.Value = newValue;
        setting.UpdatedAt = DateTime.UtcNow;
        setting.UpdatedByUserId = userId;

        await _dbContext.SaveChangesAsync();

        await _auditService.LogAsync(
            userId,
            $"Changed from \"{oldValue}\" to \"{newValue}\"",
            "System Setting",
            label);
    }

    public async Task<MyAccountDto?> GetMyAccountAsync(Guid userId)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.FullName,
                u.NationalId,
                u.Designation,
                u.MobileNumber,
                u.Email,
                u.LastLoginAt,
                RoleName = u.UserRoles.Select(r => r.Role.Name).FirstOrDefault(),
                Scopes = u.UserScopes
                    .Select(s => new
                    {
                        Constituency = s.Constituency != null ? s.Constituency.Name : null,
                        Island = s.Island != null ? s.Island.Name : null
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (user == null)
            return null;

        var scope = await _accessScopeService.GetAsync(userId);

        var scopeLines = scope.IsAdministrator
            ? new List<string> { "All constituencies and islands" }
            : user.Scopes
                .Select(s => s.Island != null
                    ? $"{s.Island}, {s.Constituency}"
                    : $"{s.Constituency} (all islands)")
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

        return new MyAccountDto
        {
            FullName = user.FullName,
            NationalId = user.NationalId,
            Designation = user.Designation,
            MobileNumber = user.MobileNumber,
            Email = user.Email,
            RoleName = HR28.Application.DTOs.Users.RoleOrder.Sort(scope.Roles).FirstOrDefault() ?? string.Empty,
            Roles = HR28.Application.DTOs.Users.RoleOrder.Sort(scope.Roles),
            Permissions = HR28.Application.Common.PermissionCatalog.All
                .Select(p => p.Key)
                .Where(scope.HasPermission)
                .ToList(),
            IsAdministrator = scope.IsAdministrator,
            VoterProfileView = scope.VoterProfileView,
            StartPage = scope.StartPage,
            SearchArea = scope.SearchArea,
            Scopes = scopeLines,
            LastLoginAt = user.LastLoginAt
        };
    }
}
