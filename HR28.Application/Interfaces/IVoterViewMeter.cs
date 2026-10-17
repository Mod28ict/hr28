namespace HR28.Application.Interfaces;

/// <summary>
/// Caps how many voters one person can view per hour and per Maldives day through the voter
/// list, search and ID-card lookup (owner decision 2026-10-17, ASVS review M1): stops anyone
/// copying the registry. Limits are in Settings → System and apply to every role.
/// </summary>
public interface IVoterViewMeter
{
    /// <summary>Null when the person may view voters now, otherwise the plain message to show.</summary>
    Task<string?> PausedMessageAsync(Guid userId);

    /// <summary>
    /// Counts voters just shown. The first time a limit is reached in its hour or day: an
    /// Audit Trail entry, a SECURITY log line and one SMS to the Administrators.
    /// </summary>
    Task RecordAsync(Guid userId, int voters);
}
