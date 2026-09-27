using HR28.Application.DTOs.Auth;

namespace HR28.Application.Interfaces;

public interface IAuthService
{
    Task<bool> GenerateOtpAsync(
        GenerateOtpRequestDto request);

    Task<LoginResponseDto> VerifyOtpAsync(
        VerifyOtpRequestDto request);
}