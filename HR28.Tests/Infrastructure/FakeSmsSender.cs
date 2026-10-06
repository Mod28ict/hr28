using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using HR28.Application.Interfaces;

namespace HR28.Tests.Infrastructure;

/// <summary>Keeps every "sent" SMS in memory so tests can read the code a user would receive.</summary>
public sealed partial class FakeSmsSender : ISmsSender
{
    private readonly ConcurrentQueue<(string Phone, string Message)> _sent = new();

    public Task<bool> SendAsync(string phoneNumber, string message)
    {
        _sent.Enqueue((phoneNumber, message));
        return Task.FromResult(true);
    }

    public IReadOnlyList<string> MessagesTo(string phone) =>
        _sent.Where(m => m.Phone == phone).Select(m => m.Message).ToList();

    /// <summary>The 6-digit code in the latest SMS to this number.</summary>
    public string LastCodeFor(string phone)
    {
        var message = MessagesTo(phone).LastOrDefault()
            ?? throw new InvalidOperationException($"No SMS was sent to {phone}.");

        return SixDigits().Match(message).Value;
    }

    [GeneratedRegex(@"\b\d{6}\b")]
    private static partial Regex SixDigits();
}
