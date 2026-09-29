namespace HR28.Web.Models.Auth;

public class VerifyOtpResponse
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;
}