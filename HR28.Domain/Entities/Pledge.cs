using HR28.Domain.Entities;

public class Pledge
{
    public Guid Id { get; set; }

    public Guid VoterId { get; set; }

    public Guid CreatedByUserId { get; set; }

    public Guid? AssignedToUserId { get; set; }

    public DateTime PledgeDate { get; set; }
        = DateTime.UtcNow;

    public string Title { get; set; }
        = string.Empty;

    public string Description { get; set; }
        = string.Empty;

    public string Status { get; set; }
        = "Open";

    public string Priority { get; set; }
        = "Medium";

    public DateTime? DueDate { get; set; }

    public DateTime? FulfilledDate { get; set; }

    public string ResolutionNotes { get; set; }
        = string.Empty;

    public Voter Voter { get; set; } = null!;

    public User CreatedByUser { get; set; } = null!;

    public User? AssignedToUser { get; set; }
}