namespace HR28.Application.DTOs.Voters;

/// <summary>Optional filters for the voter list. Always applied inside the user's areas.</summary>
public class VoterListFilter
{
    /// <summary>Matches National ID, name, phone number or island name.</summary>
    public string? SearchTerm { get; set; }

    public Guid? ConstituencyId { get; set; }

    public Guid? IslandId { get; set; }

    /// <summary>House name, matched against the address.</summary>
    public string? House { get; set; }

    /// <summary>True: the address must equal the house name ("Aage" does not match "Edherimaa Aage").</summary>
    public bool HouseExact { get; set; }

    /// <summary>Supporter, Undecided, Neutral or Opponent.</summary>
    public string? Status { get; set; }

    public Guid? PartyId { get; set; }

    /// <summary>True: only voters whose party is "Not known".</summary>
    public bool NoParty { get; set; }
}
