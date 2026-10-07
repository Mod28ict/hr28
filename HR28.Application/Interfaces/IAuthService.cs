using HR28.Application.DTOs.Auth;

namespace HR28.Application.Interfaces;

public interface IAuthService
{
    Task<GenerateOtpResultDto> GenerateOtpAsync(
        GenerateOtpRequestDto request);

    Task<LoginResponseDto> VerifyOtpAsync(
        VerifyOtpRequestDto request);

    /// <summary>"Welcome back" details for a remembered device, or null if it isn't remembered (any more).</summary>
    Task<RememberedDeviceDto?> GetRememberedDeviceAsync(string? deviceToken);

    Task ForgetDeviceAsync(string? deviceToken);
}