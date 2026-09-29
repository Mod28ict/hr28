namespace HR28.Web.Models;

public class UpdateInfluencerRelationshipDto
{
    public Guid VoterId { get; set; }

    public Guid InfluencerId { get; set; }

    public string RelationshipType { get; set; }
        = string.Empty;
}