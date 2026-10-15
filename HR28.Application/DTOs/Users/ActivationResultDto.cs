namespace HR28.Application.DTOs.Users;

/// <summary>What happened when an account was switched on.</summary>
public class ActivationResultDto
{
    /// <summary>True when the account was switched on by this request.</summary>
    public bool Activated { get; set; }

    /// <summary>First activation: a new authorization code was created for the person.</summary>
    public bool FirstActivation { get; set; }

    /// <summary>True when the account was switched off by this request (the person is told by SMS).</summary>
    public bool Deactivated { get; set; }

    /// <summary>The welcome, "active again" or "deactivated" SMS was sent.</summary>
    public bool SmsSent { get; set; }

    /// <summary>
    /// Last digits of the person's number, e.g. "•••234". The code itself is never returned:
    /// it goes only to the person by SMS (owner, 2026-10-15).
    /// </summary>
    public string MaskedMobile { get; set; } = string.Empty;
}
