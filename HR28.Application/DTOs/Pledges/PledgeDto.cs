namespace HR28.Application.DTOs.Pledges;

public class PledgeDto
{
    public Guid Id { get; set; }

    public Guid VoterId { get; set; }

    public Guid CreatedByUserId { get; set; }

    public Guid? AssignedToUserId { get; set; }

    public DateTime PledgeDate { get; set; }

    public string Title { get; set; }
        = string.Empty;

    public string Description { get; set; }
        = string.Empty;

    public string Status { get; set; }
        = string.Empty;

    public DateTime? DueDate { get; set; }

    public DateTime? FulfilledDate { get; set; }

    public string ResolutionNotes { get; set; }
        = string.Empty;
}