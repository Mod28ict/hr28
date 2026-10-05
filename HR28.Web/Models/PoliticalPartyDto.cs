namespace HR28.Web.Models;

public class PoliticalPartyDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ShortName { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    /// <summary>The Voters list opens filtered to this party.</summary>
    public bool IsDefaultFilter { get; set; }

    public int VoterCount { get; set; }

    /// <summary>"Maldivian Democratic Party (MDP)".</summary>
    public string Label => string.IsNullOrWhiteSpace(ShortName) ? Name : $"{Name} ({ShortName})";
}
