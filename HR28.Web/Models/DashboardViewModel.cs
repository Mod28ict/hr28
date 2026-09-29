namespace HR28.Web.Models;

public class DashboardViewModel
{
    public DashboardDto? Dashboard { get; set; }

    public List<RecentActivityDto> Activities { get; set; }
        = new();
    public List<VoterSearchDto> RecentVoters { get; set; }
        = new();
}