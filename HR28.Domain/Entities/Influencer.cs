namespace HR28.Domain.Entities;

public class Influencer
{
    public Guid Id { get; set; }

    public string NationalId { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string ContactNumber { get; set; } = string.Empty;

    public Guid ConstituencyId { get; set; }

    public Guid? IslandId { get; set; }

    public string Remarks { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public Constituency Constituency { get; set; } = null!;

    public Island? Island { get; set; }

    public ICollection<VoterInfluencer> Voters { get; set; }
        = new List<VoterInfluencer>();
}