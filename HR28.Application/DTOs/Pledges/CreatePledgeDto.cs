using System.ComponentModel.DataAnnotations;

namespace HR28.Application.DTOs.Pledges;

public class CreatePledgeDto
{
    public Guid VoterId { get; set; }

    [MaxLength(200, ErrorMessage = "Title can be at most 200 characters.")]
    public string Title { get; set; }
        = string.Empty;

    [MaxLength(2000, ErrorMessage = "Description can be at most 2000 characters.")]
    public string Description { get; set; }
        = string.Empty;

    /// <summary>When the pledge was made, in Maldives time (optional; default now).</summary>
    public DateTime? PledgeDate { get; set; }

    public DateTime? DueDate { get; set; }

    public Guid? AssignedToUserId { get; set; }
}