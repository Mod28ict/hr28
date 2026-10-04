namespace HR28.Application.DTOs.Pledges;

public class CreatePledgeDto
{
    public Guid VoterId { get; set; }

    public string Title { get; set; }
        = string.Empty;

    public string Description { get; set; }
        = string.Empty;

    /// <summary>When the pledge was made, in Maldives time (optional; default now).</summary>
    public DateTime? PledgeDate { get; set; }

    public DateTime? DueDate { get; set; }

    public Guid? AssignedToUserId { get; set; }
}