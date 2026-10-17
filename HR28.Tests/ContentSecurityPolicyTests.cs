extern alias web;

using web::HR28.Web.Middleware;

namespace HR28.Tests;

/// <summary>
/// The web app's Content-Security-Policy (2026-10-15): code runs only with the page's nonce;
/// inline event handlers never run.
/// </summary>
public class ContentSecurityPolicyTests
{
    private static string Directive(string policy, string name) =>
        policy.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
              .Single(d => d.StartsWith(name + " ", StringComparison.Ordinal));

    [Fact]
    public void Scripts_and_style_blocks_need_the_pages_nonce()
    {
        var policy = SecurityHeadersMiddleware.Policy("abc123");

        var scripts = Directive(policy, "script-src");
        Assert.Contains("'nonce-abc123'", scripts);
        Assert.DoesNotContain("unsafe-inline", scripts);
        Assert.DoesNotContain("unsafe-eval", scripts);

        Assert.Equal("script-src-attr 'none'", Directive(policy, "script-src-attr"));

        var styleBlocks = Directive(policy, "style-src-elem");
        Assert.Contains("'nonce-abc123'", styleBlocks);
        Assert.DoesNotContain("unsafe-inline", styleBlocks);

        Assert.Equal("object-src 'none'", Directive(policy, "object-src"));
        Assert.Equal("frame-ancestors 'none'", Directive(policy, "frame-ancestors"));
    }

    [Fact]
    public void No_outside_website_may_supply_code_or_styles()
    {
        // ASVS review M4 (2026-10-17): every library is served from this site.
        var policy = SecurityHeadersMiddleware.Policy("abc123");

        Assert.DoesNotContain("http", policy);
    }
}
