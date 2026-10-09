namespace HR28.Web.Models.Users;

/// <summary>What happened when an account was switched on (from the API).</summary>
public class ActivationResult
{
    public bool Activated { get; set; }

    /// <summary>First activation: a new authorization code was created and sent by SMS.</summary>
    public bool FirstActivation { get; set; }

    public bool SmsSent { get; set; }

    /// <summary>Only when the welcome SMS could not be sent: the code, to show once.</summary>
    public string? AuthorizationCode { get; set; }
}
