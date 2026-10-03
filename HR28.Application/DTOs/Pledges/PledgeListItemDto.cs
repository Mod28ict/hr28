namespace HR28.Application.DTOs.Pledges;

/// <summary>One row of the Pledges list: the pledge plus who it is for and who recorded it.</summary>
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
}

/// <summary>Filters for the Pledges list; always applied inside the user's areas.</summary>
public class PledgeListFilter
{
    /// <summary>Matches the pledge title, voter name or voter National ID.</summary>
    public string? SearchTerm { get; set; }

    /// <summary>Open, In Progress, Completed or Cancelled; anything else means all.</summary>
    public string? Status { get; set; }

    /// <summary>True to show only unfinished pledges whose due date has passed.</summary>
    public bool OverdueOnly { get; set; }
}
