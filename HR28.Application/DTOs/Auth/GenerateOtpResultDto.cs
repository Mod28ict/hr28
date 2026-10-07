namespace HR28.Application.DTOs.Auth;

public class GenerateOtpResultDto
{
    public bool Success { get; set; }

    /// <summary>How long the new code is valid, so the sign-in screen can show a countdown.</summary>
    public int ExpiresInSeconds { get; set; }

    /// <summary>True when the request was refused because of a cooldown, hourly limit or lockout.</summary>
    public bool IsThrottled { get; set; }

    /// <summary>Plain-language reason shown to the user when the request fails.</summary>
    public string Message { get; set; } = string.Empty;

    public int RetryAfterSeconds { get; set; }

    /// <summary>True when a remembered device key is no longer valid: forget it.</summary>
    public bool DeviceNotRecognised { get; set; }
}
