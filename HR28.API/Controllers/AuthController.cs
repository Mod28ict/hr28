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
