using System.ComponentModel.DataAnnotations;

namespace HR28.Web.Models;

public class LinkInfluencerDto
{
    [Required]
    public Guid VoterId { get; set; }

    [Required]
    [Display(Name = "Influencer")]
    public Guid InfluencerId { get; set; }

    [Required]
    [StringLength(100)]
    [Display(Name = "Relationship Type")]
    public string RelationshipType { get; set; }
        = string.Empty;
}