namespace HR28.Application.DTOs.Auth;

public class GenerateOtpRequestDto
{
    public string AuthorizationCode { get; set; } = string.Empty;

    /// <summary>A remembered device's key, used instead of the authorization code.</summary>
    public string? DeviceToken { get; set; }
}
