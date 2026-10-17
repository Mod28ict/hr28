using System.Security.Cryptography;

namespace HR28.Web.Middleware;

/// <summary>
/// Adds browser security headers to every response.
///
/// Content-Security-Policy (2026-10-15: no more 'unsafe-inline' for code): a script or a
/// &lt;style&gt; block runs only if it carries this page's random nonce, which
/// <see cref="TagHelpers.CspNonceTagHelper"/> adds to every &lt;script&gt; and &lt;style&gt;
/// the views render. Inline event handlers (onclick=…) are not allowed at all; site.js
/// handles data-autosubmit, data-href and similar attributes instead. style="…" attributes
/// (spacing, sizes) stay allowed through style-src-attr: they can't run code. Older
/// browsers without CSP level 3 fall back to style-src. No outside code (2026-10-17, ASVS
/// review M4): every library, Select2 included, is served from this site's wwwroot/lib.
/// </summary>
public class SecurityHeadersMiddleware
{
    /// <summary>HttpContext item holding this response's nonce.</summary>
    public const string NonceItem = "CspNonce";

    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public static string Policy(string nonce) =>
        "default-src 'self'; " +
        $"script-src 'self' 'nonce-{nonce}'; " +
        "script-src-attr 'none'; " +
        $"style-src 'self' 'unsafe-inline'; " +
        $"style-src-elem 'self' 'nonce-{nonce}'; " +
        "style-src-attr 'unsafe-inline'; " +
        "img-src 'self' data:; " +
        "font-src 'self' data:; " +
        "connect-src 'self'; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'";

    public Task InvokeAsync(HttpContext context)
    {
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        context.Items[NonceItem] = nonce;

        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;

            headers["Content-Security-Policy"] = Policy(nonce);
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";

            return Task.CompletedTask;
        });

        return _next(context);
    }
}
