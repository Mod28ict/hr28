namespace HR28.Web.Models;

/// <summary>An influencer with the linked voters the user may see (from GET api/influencers/{id}/voters).</summary>
public class InfluencerVotersDto
{
    public InfluencerDto Influencer { get; set; } = new();

    public PagedResult<LinkedVoterDto> Voters { get; set; } = new();

    /// <summary>Links to voters outside the user's areas: counted, never listed.</summary>
    public int OutsideAreaCount { get; set; }

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

/// <summary>Search and filters on an influencer's linked-voters page, kept in the page links.</summary>
public class LinkedVoterFilterModel
{
    public string? SearchTerm { get; set; }

    public Guid? ConstituencyId { get; set; }

    public string? Status { get; set; }

    public string? Relationship { get; set; }

    public int PageSize { get; set; } = 20;

    public List<LookupDto> Constituencies { get; set; } = new();

    public bool IsNarrowed =>
        !string.IsNullOrWhiteSpace(SearchTerm) || ConstituencyId.HasValue ||
        !string.IsNullOrWhiteSpace(Status) || !string.IsNullOrWhiteSpace(Relationship);

    public Dictionary<string, string> RouteValues()
    {
        var values = new Dictionary<string, string> { ["pageSize"] = PageSize.ToString() };

        if (!string.IsNullOrWhiteSpace(SearchTerm)) values["searchTerm"] = SearchTerm;
        if (ConstituencyId.HasValue) values["constituencyId"] = ConstituencyId.Value.ToString();
        if (!string.IsNullOrWhiteSpace(Status)) values["status"] = Status;
        if (!string.IsNullOrWhiteSpace(Relationship)) values["relationship"] = Relationship;

        return values;
    }

    public string ToApiQuery() =>
        string.Concat(RouteValues().Select(kv => $"&{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
}
