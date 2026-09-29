using HR28.Application.DTOs.Reports;
using HR28.Application.Interfaces;
using HR28.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HR28.Infrastructure.Services;

public class ReportService : IReportingService
{
    private readonly HR28DbContext _dbContext;

    public ReportService(
        HR28DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<ConstituencySummaryDto>>
        GetConstituencySummaryAsync()
    {
        var constituencies =
            await _dbContext.Constituencies
                .ToListAsync();

        var result =
            new List<ConstituencySummaryDto>();

        foreach (var constituency in constituencies)
        {
            var voterIds = await _dbContext.Voters
                .Where(v =>
                    v.ConstituencyId ==
                    constituency.Id)
                .Select(v => v.Id)
                .ToListAsync();
            var totalVoters = voterIds.Count;

            var supporters =
                await _dbContext.Voters
                    .CountAsync(v =>
                        v.ConstituencyId == constituency.Id &&
                        v.SupportStatus == "Supporter");

            var supportPercentage =
                totalVoters == 0
                    ? 0
                    : Math.Round(
                        (decimal)supporters /
                        totalVoters * 100,
                        2);

            var influencers =
                await _dbContext.Influencers
                    .CountAsync(i =>
                        i.ConstituencyId ==
                        constituency.Id);

            var encounters =
                await _dbContext.Encounters
                    .CountAsync(e =>
                        voterIds.Contains(e.VoterId));

            var pledges =
                await _dbContext.Pledges
                    .CountAsync(p =>
                        voterIds.Contains(p.VoterId));

            result.Add(
                new ConstituencySummaryDto
                {
                    ConstituencyId = constituency.Id,
                    ConstituencyName = constituency.Name,

                    TotalVoters = totalVoters,

                    Supporters = supporters,

                    SupportPercentage = supportPercentage,

                    Opponents =
                        await _dbContext.Voters
                            .CountAsync(v =>
                                v.ConstituencyId ==
                                constituency.Id &&
                                v.SupportStatus ==
                                "Opponent"),

                    Undecided =
                        await _dbContext.Voters
                            .CountAsync(v =>
                                v.ConstituencyId ==
                                constituency.Id &&
                                v.SupportStatus ==
                                "Undecided"),

                    Neutral =
                        await _dbContext.Voters
                            .CountAsync(v =>
                                v.ConstituencyId ==
                                constituency.Id &&
                                v.SupportStatus ==
                                "Neutral"),

                    TotalInfluencers =
                        await _dbContext.Influencers
                            .CountAsync(i =>
                                i.ConstituencyId ==
                                constituency.Id),

                    TotalEncounters =
                        await _dbContext.Encounters
                            .CountAsync(e =>
                                voterIds.Contains(
                                    e.VoterId)),

                    TotalPledges =
                        await _dbContext.Pledges
                            .CountAsync(p =>
                                voterIds.Contains(
                                    p.VoterId))
                });
        }

        return result
            .OrderByDescending(x => x.TotalVoters)
            .ToList();
    }
    public async Task<PledgeStatusSummaryDto>
        GetPledgeStatusSummaryAsync()
    {
        return new PledgeStatusSummaryDto
        {
            Pending = await _dbContext.Pledges
                .CountAsync(x => x.Status == "Open"),

            InProgress = await _dbContext.Pledges
                .CountAsync(x => x.Status == "In Progress"),

            Completed = await _dbContext.Pledges
                .CountAsync(x => x.Status == "Completed"),

            Cancelled = await _dbContext.Pledges
                .CountAsync(x => x.Status == "Cancelled")
        };
    }
    public async Task<List<TopInfluencerDto>>
        GetTopInfluencersAsync(int top = 10)
    {
        return await _dbContext.Influencers
            .Select(i => new TopInfluencerDto
            {
                InfluencerId = i.Id,
                FullName = i.FullName,

                LinkedVoters =
                    _dbContext.VoterInfluencers
                        .Count(v =>
                            v.InfluencerId == i.Id)
            })
            .OrderByDescending(x => x.LinkedVoters)
            .Take(top)
            .ToListAsync();
    }
}
