using HR28.Web.Middleware;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace HR28.Web.TagHelpers;

/// <summary>
/// Puts this page's Content-Security-Policy nonce on every &lt;script&gt; and &lt;style&gt;
/// the views render, so the browser runs them and nothing injected without it
/// (see <see cref="SecurityHeadersMiddleware"/>).
/// </summary>
[HtmlTargetElement("script")]
[HtmlTargetElement("style")]
public class CspNonceTagHelper : TagHelper
{
    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    // Run before other helpers on the same tag (e.g. asp-append-version).
    public override int Order => -1000;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (ViewContext.HttpContext.Items[SecurityHeadersMiddleware.NonceItem] is string nonce)
            output.Attributes.SetAttribute("nonce", nonce);
    }
}
