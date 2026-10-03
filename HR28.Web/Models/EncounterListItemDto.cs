namespace HR28.Web.Models;

/// <summary>One row of the Encounters list (from GET api/encounters).</summary>
public class EncounterListItemDto
{
    public Guid Id { get; set; }

    public DateTime EncounterDate { get; set; }

    public string EncounterType { get; set; } = string.Empty;

    public string Outcome { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public Guid VoterId { get; set; }

    public string VoterName { get; set; } = string.Empty;

    public string VoterNationalId { get; set; } = string.Empty;

    public string IslandName { get; set; } = string.Empty;

    public string ConstituencyName { get; set; } = string.Empty;

    public string RecordedBy { get; set; } = string.Empty;
}

/// <summary>Search and filters on the Encounters page, kept in the page links.</summary>
public class EncounterListFilterModel
{
    public static readonly string[] Types = { "Door Visit", "Phone Call", "Meeting", "Campaign Event", "Office Visit", "Other" };

    /// <summary>Outcome and its colour, matching the Add Encounter form.</summary>
    public static readonly (string Value, string Tone)[] Outcomes =
    {
        ("Positive", "is-green"),
        ("Undecided", "is-amber"),
        ("Negative", "is-red"),
        ("Follow-up Required", "is-blue"),
        ("No Contact", "is-muted")
    };

    public string? SearchTerm { get; set; }

    public string? Type { get; set; }

    public string? Outcome { get; set; }

    public int PageSize { get; set; } = 20;

    public bool IsNarrowed =>
        !string.IsNullOrWhiteSpace(SearchTerm) || !string.IsNullOrWhiteSpace(Type) || !string.IsNullOrWhiteSpace(Outcome);

    public Dictionary<string, string> RouteValues()
    {
        var values = new Dictionary<string, string> { ["pageSize"] = PageSize.ToString() };

        if (!string.IsNullOrWhiteSpace(SearchTerm)) values["searchTerm"] = SearchTerm;
        if (!string.IsNullOrWhiteSpace(Type)) values["type"] = Type;
        if (!string.IsNullOrWhiteSpace(Outcome)) values["outcome"] = Outcome;

        return values;
    }

    public string ToApiQuery() =>
        string.Concat(RouteValues().Select(kv => $"&{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
}
