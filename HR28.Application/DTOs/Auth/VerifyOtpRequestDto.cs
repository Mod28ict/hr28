namespace HR28.Application.DTOs.Auth;

public class VerifyOtpRequestDto
{
    public string AuthorizationCode { get; set; } = string.Empty;

    public string OtpCode { get; set; } = string.Empty;
}