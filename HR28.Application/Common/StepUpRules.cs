namespace HR28.Application.Common;

/// <summary>
/// Step-up verification ("confirm it's you", owner 2026-10-15): before sensitive actions
/// (user management, roles and rights, system settings and lists, report downloads,
/// voter and membership uploads) the person enters a fresh SMS code, even when signed in.
/// The time of that confirmation travels in the sign-in token; the API refuses sensitive
/// requests when it is missing or older than <see cref="ValidFor"/>.
/// </summary>
public static class StepUpRules
{
    /// <summary>Token claim: when the person last confirmed it's them (Unix seconds).</summary>
    public const string Claim = "stepup_time";

    /// <summary>How long one confirmation covers sensitive actions.</summary>
    public static readonly TimeSpan ValidFor = TimeSpan.FromMinutes(15);

    /// <summary>Response header on a refusal, so the web app can ask the person to confirm.</summary>
    public const string RequiredHeader = "X-Step-Up-Required";
}
