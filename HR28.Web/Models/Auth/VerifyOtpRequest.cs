namespace HR28.Web.Models.Auth;

public class VerifyOtpRequest
{
    public string AuthorizationCode { get; set; } = string.Empty;

    public string OtpCode { get; set; } = string.Empty;

    /// <summary>Signing in on a remembered device (no authorization code typed).</summary>
    public bool UseDevice { get; set; }

    /// <summary>"Remember me on this device" was ticked on the sign-in page.</summary>
    public bool RememberDevice { get; set; }

    /// <summary>Read from the device cookie; never from the form.</summary>
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public string? DeviceToken { get; set; }

    /// <summary>e.g. "Chrome on Windows", from the browser's user agent.</summary>
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public string? DeviceName { get; set; }
}