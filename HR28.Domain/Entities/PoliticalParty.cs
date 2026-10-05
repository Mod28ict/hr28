namespace HR28.Domain.Entities;

/// <summary>
/// A political party registered with the Elections Commission. Administrators keep the
/// list up to date in Settings → Lists. A voter with no party is "Not known".
/// </summary>
public class PoliticalParty
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Abbreviation shown in tables, e.g. "MDP".</summary>
    public string ShortName { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    /// <summary>The Voters list opens filtered to this party (at most one party).</summary>
    public bool IsDefaultFilter { get; set; }

    public DateTime CreatedAt { get; set; }
}
