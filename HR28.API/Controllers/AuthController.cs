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
            return BadRequest(new { message = result.Message });

        return Ok(new
        {
            message = "OTP Generated.",
            expiresInSeconds = result.ExpiresInSeconds
        });
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
