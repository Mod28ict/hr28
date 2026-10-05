namespace HR28.Web.Models;

public class InfluencerCategoryDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public int InfluencerCount { get; set; }
}

/// <summary>
/// Search and filters on the Influencers list, kept in the page links. Influencers are
/// global, so every constituency and island is offered (not only the user's areas).
/// </summary>
public class InfluencerListFilterModel
{
    /// <summary>The category filter value for "influencers with no category".</summary>
    public const string NoCategoryValue = "none";

    public string? SearchTerm { get; set; }

    public Guid? ConstituencyId { get; set; }

    public Guid? IslandId { get; set; }

    public Guid? CategoryId { get; set; }

    public bool NoCategory { get; set; }

    public int PageSize { get; set; } = 20;

    public List<LookupDto> Constituencies { get; set; } = new();

    public List<LookupDto> Islands { get; set; } = new();

    public List<InfluencerCategoryDto> Categories { get; set; } = new();

    /// <summary>The category select's value: a category id, "none" or empty.</summary>
    public string CategoryValue =>
        NoCategory ? NoCategoryValue : CategoryId?.ToString() ?? string.Empty;

    public bool IsNarrowed =>
        ConstituencyId.HasValue || IslandId.HasValue || CategoryId.HasValue || NoCategory ||
        !string.IsNullOrWhiteSpace(SearchTerm);

    /// <summary>Query values for page links, so paging keeps the search and filters.</summary>
    public Dictionary<string, string> RouteValues()
    {
        var values = new Dictionary<string, string> { ["pageSize"] = PageSize.ToString() };

        if (!string.IsNullOrWhiteSpace(SearchTerm)) values["searchTerm"] = SearchTerm;
        if (ConstituencyId.HasValue) values["constituencyId"] = ConstituencyId.Value.ToString();
        if (IslandId.HasValue) values["islandId"] = IslandId.Value.ToString();
        if (!string.IsNullOrEmpty(CategoryValue)) values["category"] = CategoryValue;

        return values;
    }

    /// <summary>The same values as an API query string (without page).</summary>
    public string ToApiQuery()
    {
        var query = $"&pageSize={PageSize}";

        if (!string.IsNullOrWhiteSpace(SearchTerm)) query += "&searchTerm=" + Uri.EscapeDataString(SearchTerm);
        if (ConstituencyId.HasValue) query += "&constituencyId=" + ConstituencyId.Value;
        if (IslandId.HasValue) query += "&islandId=" + IslandId.Value;
        if (NoCategory) query += "&noCategory=true";
        else if (CategoryId.HasValue) query += "&categoryId=" + CategoryId.Value;

        return query;
    }
}
