namespace HR28.Web.Models.Auth;

public class GenerateOtpRequest
{
    public string AuthorizationCode { get; set; } = string.Empty;

    /// <summary>"Remember me on this device" (ticked on the sign-in page).</summary>
    public bool RememberDevice { get; set; }

    /// <summary>The remembered device's key, read from its cookie; never from the form.</summary>
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public string? DeviceToken { get; set; }
}
