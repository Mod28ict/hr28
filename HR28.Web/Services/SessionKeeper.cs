using System.Text;
using System.Text.Json;

namespace HR28.Web.Services;

/// <summary>
/// Keeps a working person signed in. The API's sign-in token lasts 1 hour; while the
/// person is active it is quietly swapped for a fresh one before it runs out. After an
/// hour with no activity the page asks "Stay signed in?" (site.js), and with no answer
/// within a minute signs them out with a message. A sign-in still ends after 12 hours.
/// </summary>
public class SessionKeeper
{
    /// <summary>No activity for this long ends the session (the page warns a minute before).</summary>
    public static readonly TimeSpan IdleLimit = TimeSpan.FromHours(1);

    /// <summary>How long the "Stay signed in?" question waits for an answer.</summary>
    public static readonly TimeSpan WarningTime = TimeSpan.FromMinutes(1);

    /// <summary>Refresh the token when it has less than this left.</summary>
    private static readonly TimeSpan RefreshWhenLeft = TimeSpan.FromMinutes(20);

    /// <summary>Shown on the sign-in page after the "Stay signed in?" question went unanswered.</summary>
    public const string IdleSignOutMessage =
        "You were signed out because there was no activity for an hour. Anything you saved is kept. Please sign in again.";

    /// <summary>Shown on the sign-in page when a session ended for any other reason.</summary>
    public const string EndedMessage =
        "Your session has ended. Please sign in again.";

    private readonly ApiClient _apiClient;

    public SessionKeeper(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    /// <summary>When the token runs out (from its "exp"), or null if it can't be read.</summary>
    public static DateTime? ExpiresAtUtc(string? token)
    {
        var parts = (token ?? string.Empty).Split('.');

        if (parts.Length != 3)
            return null;

        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');

            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));

            return doc.RootElement.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var seconds)
                ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime
                : null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Swaps the session's token for a fresh 1-hour one (always when <paramref name="force"/>,
    /// otherwise only when it is close to running out). False: the person must sign in again.
    /// </summary>
    public async Task<bool> RefreshAsync(ISession session, bool force)
    {
        var token = session.GetString("JwtToken");

        if (string.IsNullOrWhiteSpace(token))
            return false;

        if (!force && ExpiresAtUtc(token) is { } expires && expires - DateTime.UtcNow > RefreshWhenLeft)
            return true;

        var result = await _apiClient.PostAsync<RefreshResult>("Auth/refresh", new { }, token);

        if (result.Success && !string.IsNullOrWhiteSpace(result.Data?.Token))
        {
            session.SetString("JwtToken", result.Data.Token);
            return true;
        }

        // A brief API hiccup keeps the current token; only a refusal ends the session.
        return !result.IsUnauthorized;
    }

    private sealed class RefreshResult
    {
        public string Token { get; set; } = string.Empty;
    }
}
