namespace HR28.Application.DTOs.Influencers;

public class UpdateInfluencerRelationshipDto
{
    public Guid VoterId { get; set; }

    public Guid InfluencerId { get; set; }

    public string RelationshipType { get; set; }
        = string.Empty;
}