namespace HR28.Application.DTOs.Users;

/// <summary>
/// "Reset code" (owner, 2026-10-15): the old code stops working and the new one goes ONLY to
/// the person by SMS. Nobody else ever sees it, administrators included (so nobody can
/// misuse someone else's credentials). The result says what happened, never the code.
/// </summary>
public class ResetCodeResultDto
{
    /// <summary>The new code was sent to the person by SMS.</summary>
    public bool SmsSent { get; set; }

    /// <summary>
    /// The account is inactive: the old code was stopped and no new one made; activating the
    /// account sends a welcome SMS with a new code.
    /// </summary>
    public bool AccountInactive { get; set; }

    /// <summary>Last digits of the person's number, e.g. "•••234".</summary>
    public string MaskedMobile { get; set; } = string.Empty;
}
