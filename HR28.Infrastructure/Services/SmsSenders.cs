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

    private static string MaskPhone(string phone) =>
        string.IsNullOrWhiteSpace(phone) || phone.Length <= 3
            ? "(no number)"
            : new string('•', phone.Length - 3) + phone[^3..];
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
