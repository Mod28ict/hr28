using HR28.Application.Common;
using HR28.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace HR28.Infrastructure.Services;

/// <summary>
/// Per phone number limit in front of the real SMS sender: at most <see cref="MaxPerHour"/>
/// messages to one number in any hour, whatever caused them (sign-in codes, welcome and
/// phone-change messages), so HR28 can't be used to flood someone's phone. Kept in memory
/// (one API server per client); a restart clears it, the per-account and per-IP limits stay.
/// </summary>
public class PhoneLimitedSmsSender : ISmsSender
{
    public const int MaxPerHour = 8;
    private static readonly TimeSpan Window = TimeSpan.FromHours(1);

    private readonly ISmsSender _inner;
    private readonly ILogger<PhoneLimitedSmsSender> _logger;
    private readonly Func<DateTime> _utcNow;
    private readonly Dictionary<string, Queue<DateTime>> _sent = new();
    private readonly object _lock = new();

    public PhoneLimitedSmsSender(ISmsSender inner, ILogger<PhoneLimitedSmsSender> logger, Func<DateTime>? utcNow = null)
    {
        _inner = inner;
        _logger = logger;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    public Task<bool> SendAsync(string phoneNumber, string message)
    {
        var key = MaldivesPhone.ToInternational(phoneNumber) ?? phoneNumber ?? string.Empty;
        var now = _utcNow();

        lock (_lock)
        {
            if (!_sent.TryGetValue(key, out var times))
                _sent[key] = times = new Queue<DateTime>();

            while (times.Count > 0 && now - times.Peek() >= Window)
                times.Dequeue();

            if (times.Count >= MaxPerHour)
            {
                _logger.LogWarning("SMS not sent: {Max} messages to the same number in the last hour.", MaxPerHour);
                return Task.FromResult(false);
            }

            times.Enqueue(now);

            // Forget numbers that have gone quiet, so the list can't grow without end.
            if (_sent.Count > 5000)
                foreach (var quiet in _sent.Where(p => p.Value.Count == 0 || now - p.Value.Last() >= Window).Select(p => p.Key).ToList())
                    _sent.Remove(quiet);
        }

        return _inner.SendAsync(phoneNumber!, message);
    }
}
