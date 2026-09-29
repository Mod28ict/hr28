namespace HR28.Web.Models;

public class ConstituencySummaryDto
{
    public Guid ConstituencyId { get; set; }

    public string ConstituencyName { get; set; } = string.Empty;

    public int TotalVoters { get; set; }

    public int Supporters { get; set; }

    public int Opponents { get; set; }

    public int Undecided { get; set; }

    public int Neutral { get; set; }

    public decimal SupportPercentage { get; set; }

    public int EngagementScore { get; set; }
}