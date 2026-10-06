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

    // Defaults apply until an administrator saves a value.
    public const string DefaultCampaignName = "Campaign Intelligence";
    public const int DefaultOtpExpiryMinutes = 5;
    public const int DefaultOtpMaxAttempts = 5;

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

        var current = await GetAsync();

        // Each changed value is saved and audited with its old and new value.
        await SaveIfChangedAsync(CampaignNameKey, "Campaign name",
            current.CampaignName, campaignName, userId);

        await SaveIfChangedAsync(OtpExpiryMinutesKey, "OTP expiry (minutes)",
            current.OtpExpiryMinutes.ToString(), settings.OtpExpiryMinutes.ToString(), userId);

        await SaveIfChangedAsync(OtpMaxAttemptsKey, "Maximum OTP attempts",
            current.OtpMaxAttempts.ToString(), settings.OtpMaxAttempts.ToString(), userId);

        return await GetAsync();
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
            Scopes = scopeLines,
            LastLoginAt = user.LastLoginAt
        };
    }
}
