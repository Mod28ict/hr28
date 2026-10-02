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
