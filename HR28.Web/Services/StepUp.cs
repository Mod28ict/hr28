namespace HR28.Web.Services;

/// <summary>
/// "Confirm it's you" (step-up, owner 2026-10-15) in the web app. The API is the boundary
/// (it refuses sensitive requests without a recent confirmation); the web app asks first
/// where it can, so people aren't sent back after filling in a form.
/// </summary>
public static class StepUp
{
    /// <summary>Session key: Unix seconds until which the last confirmation covers sensitive pages.</summary>
    public const string SessionKey = "StepUpUntil";

    /// <summary>HttpContext item set by <see cref="ApiClient"/> when the API asked for a confirmation.</summary>
    public const string RequiredItem = "StepUpRequired";

    /// <summary>Same header as the API's StepUpRules.RequiredHeader.</summary>
    public const string RequiredHeader = "X-Step-Up-Required";

    /// <summary>Slightly shorter than the API's 15 minutes, so the page asks before the API refuses.</summary>
    public static readonly TimeSpan ValidFor = TimeSpan.FromMinutes(14);

    public static bool IsFresh(HttpContext context) =>
        long.TryParse(context.Session.GetString(SessionKey), out var until) &&
        DateTimeOffset.UtcNow.ToUnixTimeSeconds() < until;

    public static void MarkFresh(HttpContext context) =>
        context.Session.SetString(SessionKey, DateTimeOffset.UtcNow.Add(ValidFor).ToUnixTimeSeconds().ToString());

    /// <summary>A local page address to come back to (never another site).</summary>
    public static string SafeReturnUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url) && url.StartsWith('/') && !url.StartsWith("//") && !url.StartsWith("/\\")
            ? url
            : "/Dashboard";

    /// <summary>The "Confirm it's you" page, coming back to <paramref name="returnUrl"/> afterwards.</summary>
    public static string PageFor(string returnUrl) =>
        "/StepUp?returnUrl=" + Uri.EscapeDataString(SafeReturnUrl(returnUrl));

    /// <summary>The page the person was on: this one for a GET, otherwise the page the form came from.</summary>
    public static string CurrentPage(HttpRequest request)
    {
        if (HttpMethods.IsGet(request.Method))
            return request.Path + request.QueryString;

        if (Uri.TryCreate(request.Headers.Referer.ToString(), UriKind.Absolute, out var referer) &&
            string.Equals(referer.Host, request.Host.Host, StringComparison.OrdinalIgnoreCase))
            return referer.PathAndQuery;

        return "/Dashboard";
    }
}
