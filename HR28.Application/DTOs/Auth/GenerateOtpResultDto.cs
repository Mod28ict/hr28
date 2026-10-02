namespace HR28.Application.DTOs.Auth;

public class GenerateOtpResultDto
{
    public bool Success { get; set; }

    /// <summary>How long the new code is valid, so the sign-in screen can show a countdown.</summary>
    public int ExpiresInSeconds { get; set; }
}
