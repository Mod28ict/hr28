namespace HR28.Web.Models.Auth;

/// <summary>"Welcome back" details for a remembered device.</summary>
public class RememberedDevice
{
    public string FirstName { get; set; } = string.Empty;

    public string MaskedMobile { get; set; } = string.Empty;
}
