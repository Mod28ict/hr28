namespace HR28.Web.Models;

public class TopInfluencerDto
{
    public Guid InfluencerId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public int LinkedVoters { get; set; }
}
