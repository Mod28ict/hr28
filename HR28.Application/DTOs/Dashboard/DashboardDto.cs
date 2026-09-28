namespace HR28.Application.DTOs.Dashboard;

public class DashboardDto
{
    public int TotalUsers { get; set; }

    public int TotalVoters { get; set; }

    public int TotalRoles { get; set; }

    public int TotalConstituencies { get; set; }

    public int TotalIslands { get; set; }
    public int Supporters { get; set; }

    public int Opponents { get; set; }

    public int Undecided { get; set; }

    public int Neutral { get; set; }
    public int TotalInfluencers { get; set; }

    public int TotalEncounters { get; set; }

    public int TotalPledges { get; set; }

    public int OpenPledges { get; set; }

    public int CompletedPledges { get; set; }
}