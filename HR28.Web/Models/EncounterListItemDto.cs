namespace HR28.Web.Models;

/// <summary>One row of the Encounters list (from GET api/encounters).</summary>
public class EncounterListItemDto
{
    public Guid Id { get; set; }

    public DateTime EncounterDate { get; set; }

    public string EncounterType { get; set; } = string.Empty;

    public string Outcome { get; set; } = string.Empty;

    public string? Response { get; set; }

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

    /// <summary>What the encounter was (mirrors the API's EncounterValues.Outcomes).</summary>
    public static readonly string[] Outcomes = { "Meet", "Call", "Request" };

    /// <summary>The voter's response and its colour: green / yellow / red.</summary>
    public static readonly (string Value, string Tone)[] Responses =
    {
        ("Supports", "is-green"),
        ("Undecided", "is-amber"),
        ("Does not support", "is-red")
    };

    /// <summary>Colour for a response; grey when there is none (old "No Contact" records).</summary>
    public static string ToneOf(string? response) =>
        Responses.FirstOrDefault(r => r.Value == response).Tone ?? "is-muted";

    public string? SearchTerm { get; set; }

    public string? Type { get; set; }

    public string? Outcome { get; set; }

    public string? Response { get; set; }

    public int PageSize { get; set; } = 20;

    public bool IsNarrowed =>
        !string.IsNullOrWhiteSpace(SearchTerm) || !string.IsNullOrWhiteSpace(Type) ||
        !string.IsNullOrWhiteSpace(Outcome) || !string.IsNullOrWhiteSpace(Response);

    public Dictionary<string, string> RouteValues()
    {
        var values = new Dictionary<string, string> { ["pageSize"] = PageSize.ToString() };

        if (!string.IsNullOrWhiteSpace(SearchTerm)) values["searchTerm"] = SearchTerm;
        if (!string.IsNullOrWhiteSpace(Type)) values["type"] = Type;
        if (!string.IsNullOrWhiteSpace(Outcome)) values["outcome"] = Outcome;
        if (!string.IsNullOrWhiteSpace(Response)) values["response"] = Response;

        return values;
    }

    public string ToApiQuery() =>
        string.Concat(RouteValues().Select(kv => $"&{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
}
