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

    /// <summary>Sends a "confirm it's you" code by SMS to a signed-in user.</summary>
    Task<StepUpSendResultDto> SendStepUpCodeAsync(Guid userId);

    /// <summary>
    /// Checks a "confirm it's you" code; on success a new token that covers sensitive actions
    /// for <see cref="Common.StepUpRules.ValidFor"/>, keeping the original sign-in time.
    /// </summary>
    Task<StepUpVerifyResultDto> VerifyStepUpAsync(Guid userId, string? code, DateTime signedInAtUtc);
}