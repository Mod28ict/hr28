namespace HR28.API.Extensions;

/// <summary>
/// Refused requests in the server log (ASVS review M2, 2026-10-17): "SECURITY:" lines the
/// server monitor can count, with who, what and from where. Request text is cleaned of
/// line breaks and control characters so nobody can forge log lines.
/// </summary>
public static class SecurityLog
{
    public static void Refused(ILogger logger, HttpContext context, string reason) =>
        logger.LogWarning("SECURITY: refused {Method} {Path} - {Reason} (user {UserId}, from {Ip})",
            context.Request.Method,
            Clean(context.Request.Path.Value),
            Clean(reason),
            context.User.GetUserId()?.ToString() ?? "unknown",
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown");

    public static string Clean(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var cleaned = new string(text.Select(c => char.IsControl(c) ? '_' : c).ToArray());
        return cleaned.Length > 200 ? cleaned[..200] : cleaned;
    }
}
