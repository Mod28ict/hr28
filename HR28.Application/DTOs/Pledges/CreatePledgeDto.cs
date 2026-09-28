namespace HR28.Application.DTOs.Pledges;

public class CreatePledgeDto
{
    public Guid VoterId { get; set; }

    public string Title { get; set; }
        = string.Empty;

    public string Description { get; set; }
        = string.Empty;

    public DateTime? DueDate { get; set; }

    public Guid? AssignedToUserId { get; set; }
}