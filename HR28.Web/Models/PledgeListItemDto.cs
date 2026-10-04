namespace HR28.Web.Models;

/// <summary>One row of the Pledges list (from GET api/pledges).</summary>
public class PledgeListItemDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string Priority { get; set; } = string.Empty;

    public DateTime PledgeDate { get; set; }

    public DateTime? DueDate { get; set; }

    public DateTime? FulfilledDate { get; set; }

    public Guid VoterId { get; set; }

    public string VoterName { get; set; } = string.Empty;

    public string VoterNationalId { get; set; } = string.Empty;

    public string IslandName { get; set; } = string.Empty;

    public string ConstituencyName { get; set; } = string.Empty;

    public string RecordedBy { get; set; } = string.Empty;

    /// <summary>Unfinished and past its due date.</summary>
    public bool IsOverdue =>
        DueDate.HasValue && DueDate.Value.Date < HR28.Web.Services.Hr28Time.Today &&
        Status != "Completed" && Status != "Cancelled";
}

/// <summary>Search and filters on the Pledges page, kept in the page links.</summary>
public class PledgeListFilterModel
{
    public string? SearchTerm { get; set; }

    public string? Status { get; set; }

    public bool Overdue { get; set; }

    public int PageSize { get; set; } = 20;

    public bool IsNarrowed => !string.IsNullOrWhiteSpace(SearchTerm) || !string.IsNullOrWhiteSpace(Status) || Overdue;

    public Dictionary<string, string> RouteValues()
    {
        var values = new Dictionary<string, string> { ["pageSize"] = PageSize.ToString() };

        if (!string.IsNullOrWhiteSpace(SearchTerm)) values["searchTerm"] = SearchTerm;
        if (!string.IsNullOrWhiteSpace(Status)) values["status"] = Status;
        if (Overdue) values["overdue"] = "true";

        return values;
    }

    public string ToApiQuery() =>
        string.Concat(RouteValues().Select(kv => $"&{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
}
