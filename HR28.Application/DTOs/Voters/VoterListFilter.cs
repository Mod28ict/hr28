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

    /// <summary>Supporter, Undecided, Neutral or Opponent.</summary>
    public string? Status { get; set; }
}
