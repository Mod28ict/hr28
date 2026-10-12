using System.Globalization;
using HR28.Application.Common;
using HR28.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace HR28.Infrastructure.Services;

/// <summary>
/// Development only: "sends" by writing to the log so developers can read their
/// code. Never registered outside the Development environment.
/// </summary>
public class DevelopmentSmsSender : ISmsSender
{
    private readonly ILogger<DevelopmentSmsSender> _logger;

    public DevelopmentSmsSender(ILogger<DevelopmentSmsSender> logger)
    {
        _logger = logger;
    }

    public Task<bool> SendAsync(string phoneNumber, string message)
    {
        _logger.LogWarning(
            "DEVELOPMENT SMS (not really sent) to {Phone}: {Message}",
            MaskPhone(phoneNumber),
            message);

        return Task.FromResult(true);
    }

    internal static string MaskPhone(string phone) =>
        string.IsNullOrWhiteSpace(phone) || phone.Length <= 3
            ? "(no number)"
            : new string('•', phone.Length - 3) + phone[^3..];
}

/// <summary>
/// TEMPORARY test mode for a live server without an SMS provider (owner decision,
/// 2026-10-12): each message is written to a file in a folder only the server's
/// administrator can read (read with <c>hr28-sms-codes</c>). Switched on only by the
/// server settings <c>Sms:ServerFile:Folder</c> and <c>Sms:ServerFile:Until</c> (last
/// Maldives day, at most 31 days ahead); after that day it refuses like
/// <see cref="UnconfiguredSmsSender"/>. Nothing in the portal can switch it on.
/// </summary>
public class ServerFileSmsSender : ISmsSender
{
    public const string FolderSetting = "Sms:ServerFile:Folder";
    public const string UntilSetting = "Sms:ServerFile:Until";
    public const int MaxDaysAhead = 31;

    private readonly string _folder;
    private readonly DateTime _lastDay;
    private readonly Func<DateTime> _maldivesToday;
    private readonly ILogger<ServerFileSmsSender> _logger;

    public ServerFileSmsSender(string folder, DateTime lastDay, ILogger<ServerFileSmsSender> logger, Func<DateTime>? maldivesToday = null)
    {
        _folder = folder;
        _lastDay = lastDay.Date;
        _logger = logger;
        _maldivesToday = maldivesToday ?? (() => MaldivesTime.Today);
    }

    /// <summary>Reads the settings; throws (the API refuses to start) if they are unusable.</summary>
    public static ServerFileSmsSender FromSettings(string folder, string until, ILogger<ServerFileSmsSender> logger)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Path.IsPathRooted(folder))
            throw new InvalidOperationException($"{FolderSetting} must be a full folder path.");
        if (!DateTime.TryParseExact(until, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var lastDay))
            throw new InvalidOperationException($"{UntilSetting} must be a date like 2026-10-19.");
        if (lastDay > MaldivesTime.Today.AddDays(MaxDaysAhead))
            throw new InvalidOperationException($"{UntilSetting} may be at most {MaxDaysAhead} days ahead.");

        return new ServerFileSmsSender(folder, lastDay, logger);
    }

    public async Task<bool> SendAsync(string phoneNumber, string message)
    {
        if (_maldivesToday().Date > _lastDay)
        {
            _logger.LogError("The temporary SMS test mode ended on {LastDay:yyyy-MM-dd}; a sign-in code could not be sent.", _lastDay);
            return false;
        }

        try
        {
            var name = $"{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.txt";
            var path = Path.Combine(_folder, name);
            var text = $"{MaldivesTime.Now:yyyy-MM-dd HH:mm:ss} (Maldives) to {DevelopmentSmsSender.MaskPhone(phoneNumber)}{Environment.NewLine}{message}{Environment.NewLine}";

            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
            if (!OperatingSystem.IsWindows())
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            await using (var stream = new FileStream(path, options))
            await using (var writer = new StreamWriter(stream))
                await writer.WriteAsync(text);

            // Never the message itself: it can contain a code.
            _logger.LogWarning("TEST MODE: SMS written to the server folder instead of being sent (until {LastDay:yyyy-MM-dd}).", _lastDay);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError("The temporary SMS test mode could not write to its folder: {Error}", ex.GetType().Name);
            return false;
        }
    }
}

/// <summary>
/// Used outside Development until a real SMS provider is connected. It refuses to
/// send and never writes the message (which may contain a code) anywhere.
/// </summary>
public class UnconfiguredSmsSender : ISmsSender
{
    private readonly ILogger<UnconfiguredSmsSender> _logger;

    public UnconfiguredSmsSender(ILogger<UnconfiguredSmsSender> logger)
    {
        _logger = logger;
    }

    public Task<bool> SendAsync(string phoneNumber, string message)
    {
        _logger.LogError("No SMS provider is configured; a sign-in code could not be sent.");

        return Task.FromResult(false);
    }
}
