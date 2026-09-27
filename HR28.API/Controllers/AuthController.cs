using HR28.Application.DTOs.Auth;
using HR28.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

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
    public async Task<IActionResult> GenerateOtp(
        GenerateOtpRequestDto request)
    {
        var result =
            await _authService.GenerateOtpAsync(request);

        if (!result)
            return BadRequest("Invalid Authorization Code.");

        return Ok("OTP Generated.");
    }

    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp(
        VerifyOtpRequestDto request)
    {
        var result =
            await _authService.VerifyOtpAsync(request);

        return Ok(result);
    }
}