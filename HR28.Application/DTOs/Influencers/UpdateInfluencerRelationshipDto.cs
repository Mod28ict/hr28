using System.ComponentModel.DataAnnotations;

namespace HR28.Application.DTOs.Influencers;

public class UpdateInfluencerRelationshipDto
{
    public Guid VoterId { get; set; }

    public Guid InfluencerId { get; set; }

    [MaxLength(50, ErrorMessage = "Relationship can be at most 50 characters.")]
    public string RelationshipType { get; set; }
        = string.Empty;
}