namespace HR28.Domain.Entities;

public class VoterInfluencer
{
    public Guid Id { get; set; }

    public Guid VoterId { get; set; }

    public Guid InfluencerId { get; set; }

    public string RelationshipType { get; set; } = string.Empty;

    public DateTime LinkedAt { get; set; } = DateTime.UtcNow;

    public Voter Voter { get; set; } = null!;

    public Influencer Influencer { get; set; } = null!;
}