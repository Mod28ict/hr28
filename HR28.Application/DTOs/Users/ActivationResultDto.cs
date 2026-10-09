namespace HR28.Application.DTOs.Users;

/// <summary>What happened when an account was switched on.</summary>
public class ActivationResultDto
{
    /// <summary>True when the account was switched on by this request.</summary>
    public bool Activated { get; set; }

    /// <summary>First activation: a new authorization code was created for the person.</summary>
    public bool FirstActivation { get; set; }

    /// <summary>The welcome (or "active again") SMS was sent.</summary>
    public bool SmsSent { get; set; }

    /// <summary>
    /// Only when the welcome SMS with the code could not be sent: the code, shown once to
    /// the administrator so it isn't lost. Empty whenever the SMS was sent.
    /// </summary>
    public string? AuthorizationCode { get; set; }
}
