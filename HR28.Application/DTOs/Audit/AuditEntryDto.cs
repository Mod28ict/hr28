namespace HR28.Application.DTOs.Audit;

public class AuditEntryDto
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public string Action { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    /// <summary>Used only for links (e.g. to a voter profile); never displayed.</summary>
    public string? EntityId { get; set; }

    /// <summary>Readable name of the record, e.g. the voter's full name.</summary>
    public string? EntityLabel { get; set; }

    public Guid? ActorId { get; set; }

    public string ActorName { get; set; } = string.Empty;
}

public class AuditQueryDto
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 25;

    /// <summary>Matches action, record type or the actor's name.</summary>
    public string? Search { get; set; }

    public string? EntityName { get; set; }

    public string? Action { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }
}

public class AuditFacetsDto
{
    public List<string> EntityNames { get; set; } = new();

    public List<string> Actions { get; set; } = new();

    /// <summary>True when the caller sees every user's actions.</summary>
    public bool IsAdministrator { get; set; }
}
