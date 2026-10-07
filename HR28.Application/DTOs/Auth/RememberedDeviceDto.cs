namespace HR28.Application.DTOs.Auth;

/// <summary>What the sign-in page shows on a remembered device.</summary>
public class RememberedDeviceDto
{
    public string FirstName { get; set; } = string.Empty;

    /// <summary>Last digits only, e.g. "•••5938".</summary>
    public string MaskedMobile { get; set; } = string.Empty;
}

public class DeviceTokenDto
{
    public string DeviceToken { get; set; } = string.Empty;
}
