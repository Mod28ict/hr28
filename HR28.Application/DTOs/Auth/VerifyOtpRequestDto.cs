namespace HR28.Application.DTOs.Auth;

public class VerifyOtpRequestDto
{
    public string AuthorizationCode { get; set; } = string.Empty;

    public string OtpCode { get; set; } = string.Empty;

    /// <summary>A remembered device's key, used instead of the authorization code.</summary>
    public string? DeviceToken { get; set; }

    /// <summary>"Remember me on this device" was ticked.</summary>
    public bool RememberDevice { get; set; }

    /// <summary>Readable label for the device, e.g. "Chrome on Windows".</summary>
    public string? DeviceName { get; set; }
}