namespace HR28.Web.Models.Auth;

public class VerifyOtpRequest
{
    public string AuthorizationCode { get; set; } = string.Empty;

    public string OtpCode { get; set; } = string.Empty;
}