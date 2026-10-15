namespace HR28.Web.Models.Users;

/// <summary>What happened when an account was switched on (from the API).</summary>
public class ActivationResult
{
    public bool Activated { get; set; }

    /// <summary>First activation: a new authorization code was created and sent by SMS.</summary>
    public bool FirstActivation { get; set; }

    /// <summary>The account was switched off (the person is told by SMS).</summary>
    public bool Deactivated { get; set; }

    public bool SmsSent { get; set; }

    /// <summary>Last digits of the person's number, e.g. "•••234" (never the code).</summary>
    public string MaskedMobile { get; set; } = string.Empty;
}
