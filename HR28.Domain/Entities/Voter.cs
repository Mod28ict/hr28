

namespace HR28.Domain.Entities;

public class Voter 
{
    public Guid Id { get; set; }

    public string NationalId { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string MobileNumber { get; set; } = string.Empty;

    public Guid ConstituencyId { get; set; }

    public Constituency Constituency { get; set; } = null!;

    public Guid? IslandId { get; set; }

    public Island? Island { get; set; }

    public string Remarks { get; set; } = string.Empty;

    /// <summary>Political party; empty means "Not known" (the default for new voters).</summary>
    public Guid? PoliticalPartyId { get; set; }

    public PoliticalParty? PoliticalParty { get; set; }

    public DateTime CreatedAt { get; set; }
    public string SupportStatus { get; set; }
    = "Undecided";
    public ICollection<Encounter> Encounters { get; set; }
    = new List<Encounter>();

    public ICollection<Pledge> Pledges { get; set; }
        = new List<Pledge>();

    public ICollection<VoterInfluencer> Influencers { get; set; }
        = new List<VoterInfluencer>();
    public string Gender { get; set; }
    = string.Empty;

    /// <summary>Optional (owner decision, 2026-10-05); filled from party membership lists or typed in.</summary>
    public DateOnly? DateOfBirth { get; set; }

    public string? Ward { get; set; }

    public string RegisteredIsland { get; set; }
        = string.Empty;

    public string AtollCode { get; set; }
        = string.Empty;

    public string ConstituencyCode { get; set; }
        = string.Empty;

    public string ConstituencyName { get; set; }
        = string.Empty;
}