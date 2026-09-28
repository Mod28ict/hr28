public class LinkInfluencerDto
{
    public Guid VoterId { get; set; }

    public Guid InfluencerId { get; set; }

    public string RelationshipType { get; set; }
        = string.Empty;
}