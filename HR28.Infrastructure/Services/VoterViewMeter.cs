using HR28.Application.Common;
using HR28.Application.Interfaces;
using HR28.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace HR28.Infrastructure.Services;

/// <summary>
/// Voters viewed per person, per clock hour and per day in Maldives time (see
/// <see cref="IVoterViewMeter"/>). Counts live in memory: one API instance per client.
/// Raising a limit in Settings lifts a pause at once.
/// </summary>
public class VoterViewMeter : IVoterViewMeter
{
    private sealed class Counter
    {
        public int Viewed;
        public int Alerted;
    }

    private static readonly object Gate = new();

    private readonly IMemoryCache _cache;
    private readonly ISystemSettingsService _settings;
    private readonly IAuditService _auditService;
    private readonly ISmsSender _smsSender;
    private readonly HR28DbContext _dbContext;
    private readonly ILogger<VoterViewMeter> _logger;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public VoterViewMeter(
        IMemoryCache cache,
        ISystemSettingsService settings,
        IAuditService auditService,
        ISmsSender smsSender,
        HR28DbContext dbContext,
        ILogger<VoterViewMeter> logger,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _cache = cache;
        _settings = settings;
        _auditService = auditService;
        _smsSender = smsSender;
        _dbContext = dbContext;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    private Counter CounterFor(Guid userId, string window, TimeSpan keep)
    {
        lock (Gate)
        {
            return _cache.GetOrCreate($"voter-views:{userId}:{window}", entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = keep;
                return new Counter();
            })!;
        }
    }

    private (Counter Hour, Counter Day, DateTime Now) Counters(Guid userId)
    {
        var now = MaldivesTime.Now;

        return (CounterFor(userId, $"h{now:yyyyMMddHH}", TimeSpan.FromHours(2)),
                CounterFor(userId, $"d{now:yyyyMMdd}", TimeSpan.FromHours(26)),
                now);
    }

    public async Task<string?> PausedMessageAsync(Guid userId)
    {
        var settings = await _settings.GetAsync();
        var (hour, day, now) = Counters(userId);

        if (Volatile.Read(ref day.Viewed) >= settings.VoterViewsPerDay)
            return $"You have viewed {settings.VoterViewsPerDay:N0} voters today, so viewing voters is paused until tomorrow. " +
                   "If you need more, ask your administrator.";

        if (Volatile.Read(ref hour.Viewed) >= settings.VoterViewsPerHour)
            return $"You have viewed {settings.VoterViewsPerHour:N0} voters this hour, so viewing voters is paused until " +
                   $"{now.Date.AddHours(now.Hour + 1):HH:mm}. If you need more, ask your administrator.";

        return null;
    }

    public async Task RecordAsync(Guid userId, int voters)
    {
        if (voters <= 0)
            return;

        var settings = await _settings.GetAsync();
        var (hour, day, now) = Counters(userId);

        var dayTotal = Interlocked.Add(ref day.Viewed, voters);
        var hourTotal = Interlocked.Add(ref hour.Viewed, voters);

        // Only the request that crosses a limit raises the alert, once per hour / day.
        if (dayTotal >= settings.VoterViewsPerDay!.Value && Interlocked.Exchange(ref day.Alerted, 1) == 0)
            await AlertAsync(userId, $"{dayTotal:N0} voters today (limit {settings.VoterViewsPerDay:N0} a day)", "until tomorrow");
        else if (hourTotal >= settings.VoterViewsPerHour!.Value && Interlocked.Exchange(ref hour.Alerted, 1) == 0)
            await AlertAsync(userId, $"{hourTotal:N0} voters this hour (limit {settings.VoterViewsPerHour:N0} an hour)",
                $"until {now.Date.AddHours(now.Hour + 1):HH:mm}");
    }

    private async Task AlertAsync(Guid userId, string viewed, string until)
    {
        var name = await _dbContext.Users
            .Where(u => u.Id == userId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync() ?? "Someone";

        _logger.LogWarning("SECURITY: voter viewing paused after {Viewed} (user {UserId}, from {Ip})",
            viewed, userId, _httpContextAccessor?.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown");

        await _auditService.LogAsync(userId, $"Voter viewing paused {until}: viewed {viewed}", "User", userId.ToString());

        var admins = await _dbContext.UserRoles
            .Where(ur => ur.Role.Name == AccessScopeService.SuperAdministratorRole && ur.User.IsActive)
            .Select(ur => ur.User.MobileNumber)
            .Distinct()
            .ToListAsync();

        var shortName = (await _settings.GetBrandingAsync()).ShortName;
        var text = $"{shortName}: {name} viewed {viewed}. Their voter viewing is paused {until}. " +
                   "If this isn't expected, check the Audit Trail.";

        foreach (var phone in admins.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            try
            {
                await _smsSender.SendAsync(phone, text);
            }
            catch (Exception ex)
            {
                // The pause and the audit entry stand even if a message can't go out.
                _logger.LogError(ex, "Could not send the voter-viewing alert SMS.");
            }
        }
    }
}
