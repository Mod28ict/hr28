namespace HR28.Web.Models;

/// <summary>Search and filters on the Voters list, kept in the page links.</summary>
public class VoterListFilterModel
{
    public static readonly string[] Statuses = { "Supporter", "Undecided", "Neutral", "Opponent" };

    public string? SearchTerm { get; set; }

    public Guid? ConstituencyId { get; set; }

    public Guid? IslandId { get; set; }

    public string? House { get; set; }

    /// <summary>Exact house-name match ("Aage" does not match "Edherimaa Aage").</summary>
    public bool HouseExact { get; set; }

    public string? Status { get; set; }

    /// <summary>Party filter values besides a party id.</summary>
    public const string AllParties = "all";
    public const string PartyNotKnown = "none";

    /// <summary>A party id, "all" or "none". Always set, so page links keep it.</summary>
    public string Party { get; set; } = AllParties;

    public Guid? PartyId => Guid.TryParse(Party, out var id) ? id : null;

    public bool NoParty => Party == PartyNotKnown;

    public List<PoliticalPartyDto> Parties { get; set; } = new();

    /// <summary>
    /// The party filter from the address: missing → the party the list opens on
    /// (set in Settings → Lists, MDP by default); unknown values → all parties.
    /// </summary>
    public static string ResolveParty(string? requested, IEnumerable<PoliticalPartyDto> parties)
    {
        var list = parties.ToList();

        if (requested == null)
            return list.FirstOrDefault(p => p.IsDefaultFilter)?.Id.ToString() ?? AllParties;

        if (requested is AllParties or PartyNotKnown)
            return requested;

        return Guid.TryParse(requested, out var id) && list.Any(p => p.Id == id)
            ? id.ToString()
            : AllParties;
    }

    public int PageSize { get; set; } = 20;

    public List<LookupDto> Constituencies { get; set; } = new();

    public List<LookupDto> Islands { get; set; } = new();

    /// <summary>"Dhaandhoo Dhaairaa (G03)", or just the name when there is no code.</summary>
    public static string Label(LookupDto item) =>
        string.IsNullOrWhiteSpace(item.Code) ? item.Name : $"{item.Name} ({item.Code.Trim()})";

    /// <summary>True when any filter (not the plain search) is set.</summary>
    public bool HasFilters =>
        ConstituencyId.HasValue || IslandId.HasValue ||
        !string.IsNullOrWhiteSpace(House) || !string.IsNullOrWhiteSpace(Status) ||
        Party != AllParties;

    public bool IsNarrowed => HasFilters || !string.IsNullOrWhiteSpace(SearchTerm);

    /// <summary>Query values for page links, so paging keeps the search and filters.</summary>
    public Dictionary<string, string> RouteValues()
    {
        var values = new Dictionary<string, string> { ["pageSize"] = PageSize.ToString() };

        if (!string.IsNullOrWhiteSpace(SearchTerm)) values["searchTerm"] = SearchTerm;
        if (ConstituencyId.HasValue) values["constituencyId"] = ConstituencyId.Value.ToString();
        if (IslandId.HasValue) values["islandId"] = IslandId.Value.ToString();
        if (!string.IsNullOrWhiteSpace(House)) values["house"] = House;
        if (!string.IsNullOrWhiteSpace(House) && HouseExact) values["houseExact"] = "true";
        if (!string.IsNullOrWhiteSpace(Status)) values["status"] = Status;
        values["party"] = Party;

        return values;
    }

    /// <summary>The same values as an API query string (without page).</summary>
    public string ToApiQuery()
    {
        var values = RouteValues();
        values.Remove("party");

        var query = string.Concat(values.Select(kv => $"&{kv.Key}={Uri.EscapeDataString(kv.Value)}"));

        if (NoParty) query += "&noParty=true";
        else if (PartyId.HasValue) query += "&partyId=" + PartyId.Value;

        return query;
    }
}
