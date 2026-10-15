namespace HR28.Web.Models.Auth;

public class VerifyOtpResponse
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;
    public Guid UserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;

    /// <summary>All of the user's roles; permissions combine.</summary>
    public List<string> Roles { get; set; } = new();

    /// <summary>New device key when "Remember me" was ticked; stored only in an HttpOnly cookie.</summary>
    public string? DeviceToken { get; set; }

    public DateTime? DeviceExpiresAt { get; set; }

    /// <summary>A new browser key for sign-in alerts (kept only in an HttpOnly cookie).</summary>
    public string? KnownDeviceToken { get; set; }

    /// <summary>The remembered device isn't valid any more: forget the cookie.</summary>
    public bool DeviceNotRecognised { get; set; }
}