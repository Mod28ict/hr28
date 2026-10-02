namespace HR28.Web.Models;

public class AuditEntryDto
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public string Action { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public string? EntityId { get; set; }

    public string? EntityLabel { get; set; }

    public Guid? ActorId { get; set; }

    public string ActorName { get; set; } = string.Empty;
}

public class AuditFacetsDto
{
    public List<string> EntityNames { get; set; } = new();

    public List<string> Actions { get; set; } = new();

    public bool IsAdministrator { get; set; }
}

public class AuditFilter
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 25;

    public string? Search { get; set; }

    public string? EntityName { get; set; }

    public string? ActionName { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    public bool HasFilters =>
        !string.IsNullOrWhiteSpace(Search) ||
        !string.IsNullOrWhiteSpace(EntityName) ||
        !string.IsNullOrWhiteSpace(ActionName) ||
        From.HasValue ||
        To.HasValue;
}

public class AuditIndexViewModel
{
    public AuditFilter Filter { get; set; } = new();

    public AuditFacetsDto Facets { get; set; } = new();

    public PagedResult<AuditEntryDto> Result { get; set; } = new();
}
