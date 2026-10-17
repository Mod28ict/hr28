using System.ComponentModel.DataAnnotations;

public class LinkInfluencerDto
{
    public Guid VoterId { get; set; }

    public Guid InfluencerId { get; set; }

    [MaxLength(50, ErrorMessage = "Relationship can be at most 50 characters.")]
    public string RelationshipType { get; set; }
        = string.Empty;
}