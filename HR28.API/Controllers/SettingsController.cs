using HR28.API.Extensions;
using HR28.Application.DTOs.Settings;
using HR28.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HR28.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly ISystemSettingsService _settingsService;

    public SettingsController(ISystemSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    /// <summary>Readable by every signed-in user (the campaign name appears in the app shell).</summary>
    [HttpGet("system")]
    public async Task<IActionResult> GetSystem()
    {
        return Ok(await _settingsService.GetAsync());
    }

    /// <summary>The client's branding. Public: the sign-in pages show it before anyone signs in.</summary>
    [HttpGet("branding")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBranding()
    {
        return Ok(await _settingsService.GetBrandingAsync());
    }

    /// <summary>The client's logo (public, like the name). 404 when there is none.</summary>
    [HttpGet("logo")]
    [AllowAnonymous]
    public async Task<IActionResult> GetLogo()
    {
        var logo = await _settingsService.GetLogoAsync();

        if (logo == null)
            return NotFound();

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(logo.Value.Content, logo.Value.ContentType);
    }

    /// <summary>Uploads or replaces the logo (JPG or PNG, up to 1 MB; cleaned like voter photos).</summary>
    [HttpPost("logo")]
    [Authorize(Policy = AuthorizationPolicies.Administrator)]
    [RequestSizeLimit(HR28.Infrastructure.Services.SystemSettingsService.MaxLogoBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = HR28.Infrastructure.Services.SystemSettingsService.MaxLogoBytes + 64 * 1024)]
    public async Task<IActionResult> UploadLogo(IFormFile? file)
    {
        var userId = User.GetUserId();

        if (userId == null)
            return Unauthorized();

        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Please choose a logo first." });

        if (file.Length > HR28.Infrastructure.Services.SystemSettingsService.MaxLogoBytes)
            return BadRequest(new { message = "The logo is too large. Please choose one under 1 MB." });

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);

        var error = await _settingsService.SaveLogoAsync(stream.ToArray(), userId.Value);

        return error == null ? NoContent() : BadRequest(new { message = error });
    }

    [HttpDelete("logo")]
    [Authorize(Policy = AuthorizationPolicies.Administrator)]
    public async Task<IActionResult> RemoveLogo()
    {
        var userId = User.GetUserId();

        if (userId == null)
            return Unauthorized();

        await _settingsService.RemoveLogoAsync(userId.Value);
        return NoContent();
    }

    [HttpPut("system")]
    [Authorize(Policy = AuthorizationPolicies.Administrator)]
    public async Task<IActionResult> UpdateSystem([FromBody] SystemSettingsDto request)
    {
        var userId = User.GetUserId();

        if (userId == null)
            return Unauthorized();

        return Ok(await _settingsService.UpdateAsync(request, userId.Value));
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyAccount()
    {
        var userId = User.GetUserId();

        if (userId == null)
            return Unauthorized();

        var account = await _settingsService.GetMyAccountAsync(userId.Value);

        return account == null ? NotFound() : Ok(account);
    }
}
