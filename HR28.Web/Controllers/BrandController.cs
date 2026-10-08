using HR28.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace HR28.Web.Controllers;

/// <summary>The client's logo, for the sign-in pages and the sidebar (no sign-in needed).</summary>
public class BrandController : Controller
{
    private readonly BrandingService _branding;

    public BrandController(BrandingService branding)
    {
        _branding = branding;
    }

    /// <summary>The address carries the logo's version, so a changed logo is fetched again.</summary>
    [HttpGet]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Logo(string? v)
    {
        var logo = await _branding.GetLogoAsync();

        if (logo == null)
            return NotFound();

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(logo.Value.Content, logo.Value.ContentType);
    }
}
