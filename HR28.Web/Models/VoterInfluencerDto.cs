namespace HR28.Web.Models;

public class VoterInfluencerDto
{
    public Guid InfluencerId { get; set; }

    public string FullName { get; set; }
        = string.Empty;

    public string NationalId { get; set; }
        = string.Empty;

    public string ContactNumber { get; set; }
        = string.Empty;

    public string RelationshipType { get; set; }
        = string.Empty;
}