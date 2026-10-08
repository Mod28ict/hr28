using HR28.API.Extensions;
using HR28.Application.DTOs.Auth;
using HR28.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HR28.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("generate-otp")]
    [EnableRateLimiting(RateLimitPolicies.OtpRequest)]
    public async Task<IActionResult> GenerateOtp(
        GenerateOtpRequestDto request)
    {
        var result =
            await _authService.GenerateOtpAsync(request);

        if (result.IsThrottled)
        {
            Response.Headers.RetryAfter = result.RetryAfterSeconds.ToString();

            return StatusCode(
                StatusCodes.Status429TooManyRequests,
                new { message = result.Message });
        }

        if (!result.Success)
            return BadRequest(new { message = result.Message, deviceNotRecognised = result.DeviceNotRecognised });

        return Ok(new
        {
            message = "OTP Generated.",
            expiresInSeconds = result.ExpiresInSeconds
        });
    }

    /// <summary>Sign-in page options that don't need an account (shown before signing in).</summary>
    [HttpGet("options")]
    public async Task<IActionResult> Options([FromServices] ISystemSettingsService settings) =>
        Ok(new { rememberDeviceDays = (await settings.GetAsync()).RememberDeviceDays });

    /// <summary>"Welcome back" on a remembered device. Rate-limited like code checks.</summary>
    [HttpPost("device")]
    [EnableRateLimiting(RateLimitPolicies.OtpVerify)]
    public async Task<IActionResult> RememberedDevice(DeviceTokenDto request)
    {
        var device = await _authService.GetRememberedDeviceAsync(request.DeviceToken);

        return device == null
            ? NotFound(new { message = "This device is no longer remembered." })
            : Ok(device);
    }

    /// <summary>"Not you? Forget this device": the key stops working at once.</summary>
    [HttpPost("forget-device")]
    [EnableRateLimiting(RateLimitPolicies.OtpVerify)]
    public async Task<IActionResult> ForgetDevice(DeviceTokenDto request)
    {
        await _authService.ForgetDeviceAsync(request.DeviceToken);

        return NoContent();
    }

    /// <summary>
    /// Longest a sign-in lasts, even with constant use; after this a fresh sign-in
    /// (with the SMS code) is needed.
    /// </summary>
    public static readonly TimeSpan MaxSessionLength = TimeSpan.FromHours(12);

    /// <summary>
    /// A new 1-hour token for an active, signed-in user (the web app calls this while the
    /// person is working, and when they choose "Stay signed in"). Deactivated accounts
    /// are refused by the default policy; sessions older than 12 hours must sign in again.
    /// </summary>
    [HttpPost("refresh")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> Refresh(
        [FromServices] ITokenService tokenService,
        [FromServices] HR28.Infrastructure.Data.HR28DbContext dbContext)
    {
        var userId = User.GetUserId();

        if (userId == null ||
            !long.TryParse(User.FindFirst(ITokenService.SignedInAtClaim)?.Value, out var signedInUnix))
            return Unauthorized(new { message = "Please sign in again." });

        var signedInAt = DateTimeOffset.FromUnixTimeSeconds(signedInUnix).UtcDateTime;

        if (DateTime.UtcNow - signedInAt > MaxSessionLength)
            return Unauthorized(new { message = "For your security, please sign in again." });

        var user = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .FirstOrDefaultAsync(dbContext.Users, u => u.Id == userId.Value && u.IsActive);

        if (user == null)
            return Unauthorized(new { message = "Please sign in again." });

        return Ok(new { token = await tokenService.GenerateTokenAsync(user, signedInAt) });
    }

    [HttpPost("verify-otp")]
    [EnableRateLimiting(RateLimitPolicies.OtpVerify)]
    public async Task<IActionResult> VerifyOtp(
        VerifyOtpRequestDto request)
    {
        var result =
            await _authService.VerifyOtpAsync(request);

        return Ok(result);
    }
}
