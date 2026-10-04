using HR28.Application.DTOs.Reports;
using HR28.Application.Interfaces;
using HR28.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HR28.Infrastructure.Services;

public class ReportService : IReportingService
{
    private readonly HR28DbContext _dbContext;
    private readonly IAccessScopeService _accessScopeService;

    public ReportService(
        HR28DbContext dbContext,
        IAccessScopeService accessScopeService)
    {
        _dbContext = dbContext;
        _accessScopeService = accessScopeService;
    }

    public async Task<List<ConstituencySummaryDto>>
        GetConstituencySummaryAsync(Guid userId)
    {
        var scope = await _accessScopeService.GetAsync(userId);

        var voters = _dbContext.Voters.AsNoTracking().InScope(scope);

        var constituencies = await _dbContext.Constituencies
            .AsNoTracking()
            .InScope(scope)
            .Select(c => new { c.Id, c.Name, c.Code })
            .ToListAsync();

        // One grouped query per measure instead of several queries per constituency.
        var statusCounts = await voters
            .GroupBy(v => new { v.ConstituencyId, v.SupportStatus })
            .Select(g => new { g.Key.ConstituencyId, g.Key.SupportStatus, Count = g.Count() })
            .ToListAsync();

        // Influencers are global; rows are still limited to the user's constituencies.
        var influencerCounts = await _dbContext.Influencers
            .AsNoTracking()
            .GroupBy(i => i.ConstituencyId)
            .Select(g => new { ConstituencyId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ConstituencyId, x => x.Count);

        var encounterCounts = await _dbContext.Encounters
            .Join(voters, e => e.VoterId, v => v.Id, (e, v) => v.ConstituencyId)
            .GroupBy(id => id)
            .Select(g => new { ConstituencyId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ConstituencyId, x => x.Count);

        var pledgeCounts = await _dbContext.Pledges
            .Join(voters, p => p.VoterId, v => v.Id, (p, v) => v.ConstituencyId)
            .GroupBy(id => id)
            .Select(g => new { ConstituencyId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ConstituencyId, x => x.Count);

        int StatusCount(Guid constituencyId, string status) =>
            statusCounts
                .Where(x => x.ConstituencyId == constituencyId && x.SupportStatus == status)
                .Sum(x => x.Count);

        var result = constituencies
            .Select(c =>
            {
                var total = statusCounts
                    .Where(x => x.ConstituencyId == c.Id)
                    .Sum(x => x.Count);

                var supporters = StatusCount(c.Id, "Supporter");
                var encounters = encounterCounts.GetValueOrDefault(c.Id);
                var pledges = pledgeCounts.GetValueOrDefault(c.Id);

                return new ConstituencySummaryDto
                {
                    ConstituencyId = c.Id,
                    ConstituencyName = c.Name,
                    ConstituencyCode = c.Code?.Trim() ?? string.Empty,
                    TotalVoters = total,
                    Supporters = supporters,
                    Opponents = StatusCount(c.Id, "Opponent"),
                    Undecided = StatusCount(c.Id, "Undecided"),
                    Neutral = StatusCount(c.Id, "Neutral"),
                    SupportPercentage = total == 0
                        ? 0
                        : Math.Round((decimal)supporters / total * 100, 2),
                    TotalInfluencers = influencerCounts.GetValueOrDefault(c.Id),
                    TotalEncounters = encounters,
                    TotalPledges = pledges,
                    // Recorded interactions with voters in this constituency.
                    EngagementScore = encounters + pledges
                };
            })
            .OrderByDescending(x => x.TotalVoters)
            .ThenBy(x => x.ConstituencyName)
            .ToList();

        return result;
    }

    public async Task<PledgeStatusSummaryDto>
        GetPledgeStatusSummaryAsync(Guid userId)
    {
        var scope = await _accessScopeService.GetAsync(userId);

        var counts = await _dbContext.Pledges
            .AsNoTracking()
            .InScope(scope, _dbContext.Voters)
            .GroupBy(p => p.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count);

        return new PledgeStatusSummaryDto
        {
            Pending = counts.GetValueOrDefault("Open"),
            InProgress = counts.GetValueOrDefault("In Progress"),
            Completed = counts.GetValueOrDefault("Completed"),
            Cancelled = counts.GetValueOrDefault("Cancelled")
        };
    }

    public async Task<List<TopInfluencerDto>>
        GetTopInfluencersAsync(Guid userId, int top = 10)
    {
        var scope = await _accessScopeService.GetAsync(userId);

        var voterIds = _dbContext.Voters.InScope(scope).Select(v => v.Id);

        // Influencers are global, but only linked voters the user may see are counted.
        return await _dbContext.Influencers
            .AsNoTracking()
            .Select(i => new TopInfluencerDto
            {
                InfluencerId = i.Id,
                FullName = i.FullName,

                // Only count voters the user is allowed to see.
                LinkedVoters = i.Voters.Count(link => voterIds.Contains(link.VoterId))
            })
            .Where(x => x.LinkedVoters > 0)
            .OrderByDescending(x => x.LinkedVoters)
            .ThenBy(x => x.FullName)
            .Take(top)
            .ToListAsync();
    }
}
