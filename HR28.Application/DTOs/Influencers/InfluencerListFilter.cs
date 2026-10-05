namespace HR28.Application.DTOs.Influencers;

/// <summary>
/// Filters for the Influencers list. Influencers are global, so these narrow the
/// whole list (not the user's areas).
/// </summary>
public class InfluencerListFilter
{
    /// <summary>Matches name, National ID, phone number or island name.</summary>
    public string? SearchTerm { get; set; }

    public Guid? ConstituencyId { get; set; }

    public Guid? IslandId { get; set; }

    public Guid? CategoryId { get; set; }

    /// <summary>True: only influencers with no category.</summary>
    public bool NoCategory { get; set; }
}
