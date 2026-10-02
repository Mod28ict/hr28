using HR28.Application.DTOs.Reports;

namespace HR28.Application.Interfaces;

/// <summary>
/// Campaign reports. Every method is limited to the user's scope;
/// administrators see national figures.
/// </summary>
public interface IReportingService
{
    Task<List<ConstituencySummaryDto>>
        GetConstituencySummaryAsync(Guid userId);

    Task<PledgeStatusSummaryDto>
        GetPledgeStatusSummaryAsync(Guid userId);

    Task<List<TopInfluencerDto>>
        GetTopInfluencersAsync(Guid userId, int top = 10);
}
