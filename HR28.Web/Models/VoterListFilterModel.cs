namespace HR28.Web.Models;

/// <summary>Search and filters on the Voters list, kept in the page links.</summary>
public class VoterListFilterModel
{
    public static readonly string[] Statuses = { "Supporter", "Undecided", "Neutral", "Opponent" };

    public string? SearchTerm { get; set; }

    public Guid? ConstituencyId { get; set; }

    public Guid? IslandId { get; set; }

    public string? House { get; set; }

    public string? Status { get; set; }

    public int PageSize { get; set; } = 20;

    public List<LookupDto> Constituencies { get; set; } = new();

    public List<LookupDto> Islands { get; set; } = new();

    /// <summary>True when any filter (not the plain search) is set.</summary>
    public bool HasFilters =>
        ConstituencyId.HasValue || IslandId.HasValue ||
        !string.IsNullOrWhiteSpace(House) || !string.IsNullOrWhiteSpace(Status);

    public bool IsNarrowed => HasFilters || !string.IsNullOrWhiteSpace(SearchTerm);

    /// <summary>Query values for page links, so paging keeps the search and filters.</summary>
    public Dictionary<string, string> RouteValues()
    {
        var values = new Dictionary<string, string> { ["pageSize"] = PageSize.ToString() };

        if (!string.IsNullOrWhiteSpace(SearchTerm)) values["searchTerm"] = SearchTerm;
        if (ConstituencyId.HasValue) values["constituencyId"] = ConstituencyId.Value.ToString();
        if (IslandId.HasValue) values["islandId"] = IslandId.Value.ToString();
        if (!string.IsNullOrWhiteSpace(House)) values["house"] = House;
        if (!string.IsNullOrWhiteSpace(Status)) values["status"] = Status;

        return values;
    }

    /// <summary>The same values as an API query string (without page).</summary>
    public string ToApiQuery() =>
        string.Concat(RouteValues().Select(kv => $"&{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
}
