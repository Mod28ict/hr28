using HR28.Application.DTOs.Common;

namespace HR28.Application.DTOs.Influencers;

/// <summary>An influencer with the voters linked to them that the user may see.</summary>
public class InfluencerVotersDto
{
    public InfluencerDto Influencer { get; set; } = new();

    public PagedResult<LinkedVoterDto> Voters { get; set; } = new();

    /// <summary>Links to voters outside the user's areas: counted, never listed.</summary>
    public int OutsideAreaCount { get; set; }

    /// <summary>Relationship types used by this influencer's visible links, for the filter.</summary>
    public List<string> RelationshipTypes { get; set; } = new();
}

public class LinkedVoterDto
{
    public Guid VoterId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string NationalId { get; set; } = string.Empty;

    public string MobileNumber { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string IslandName { get; set; } = string.Empty;

    public string ConstituencyName { get; set; } = string.Empty;

    public string SupportStatus { get; set; } = string.Empty;

    public string RelationshipType { get; set; } = string.Empty;

    public DateTime LinkedAt { get; set; }
}

/// <summary>Filters for an influencer's linked voters; always inside the user's areas.</summary>
public class LinkedVoterFilter
{
    /// <summary>Matches voter name, National ID, phone or island.</summary>
    public string? SearchTerm { get; set; }

    public Guid? ConstituencyId { get; set; }

    public string? Status { get; set; }

    public string? Relationship { get; set; }
}
