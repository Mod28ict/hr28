namespace HR28.Application.DTOs.Users;

/// <summary>
/// "Reset code" (owner, 2026-10-15): the new code goes to the person by SMS and is also shown
/// once to the administrator. It is never readable again afterwards.
/// </summary>
public class ResetCodeResultDto
{
    /// <summary>The new code, shown once.</summary>
    public string AuthorizationCode { get; set; } = string.Empty;

    /// <summary>The SMS with the new code was sent (only to active accounts).</summary>
    public bool SmsSent { get; set; }

    /// <summary>The account is inactive: no SMS; the administrator gives the code when it is active again.</summary>
    public bool AccountInactive { get; set; }

    /// <summary>Last digits of the number the SMS went to, e.g. "•••234".</summary>
    public string MaskedMobile { get; set; } = string.Empty;
}
