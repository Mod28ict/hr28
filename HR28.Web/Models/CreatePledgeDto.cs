using System.ComponentModel.DataAnnotations;

namespace HR28.Web.Models;

public class CreatePledgeDto
{
    [Required]
    public Guid VoterId { get; set; }

    public Guid? AssignedToUserId { get; set; }

    [Required]
    [Display(Name = "Pledge Date")]
    public DateTime PledgeDate { get; set; }
        = DateTime.Now;

    [Required]
    [StringLength(200)]
    public string Title { get; set; }
        = string.Empty;

    [Required]
    [StringLength(2000)]
    public string Description { get; set; }
        = string.Empty;

    [Display(Name = "Due Date")]
    public DateTime? DueDate { get; set; }
}