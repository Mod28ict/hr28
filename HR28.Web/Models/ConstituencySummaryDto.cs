namespace HR28.Web.Models;

public class ConstituencySummaryDto
{
    public Guid ConstituencyId { get; set; }

    public string ConstituencyName { get; set; } = string.Empty;

    /// <summary>Constituency code, e.g. "F03"; empty when there is none.</summary>
    public string ConstituencyCode { get; set; } = string.Empty;

    public int TotalVoters { get; set; }

    public int Supporters { get; set; }

    public int Opponents { get; set; }

    public int Undecided { get; set; }

    public int Neutral { get; set; }

    public decimal SupportPercentage { get; set; }

    /// <summary>Encounters plus pledges recorded for voters in the constituency.</summary>
    public int EngagementScore { get; set; }

    public int TotalInfluencers { get; set; }

    public int TotalEncounters { get; set; }

    public int TotalPledges { get; set; }
}
