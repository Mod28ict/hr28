namespace HR28.Web.Models;

public class DashboardViewModel
{
    public DashboardDto? Dashboard { get; set; }

    public List<RecentActivityDto> Activities { get; set; }
        = new();
    public List<VoterSearchDto> RecentVoters { get; set; }
        = new();

    // Only populated for national roles; the summary endpoint is not scope-filtered.
    public List<ConstituencySummaryDto> Constituencies { get; set; }
        = new();
}