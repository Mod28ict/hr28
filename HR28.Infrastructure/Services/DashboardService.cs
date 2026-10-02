using HR28.Application.DTOs.Dashboard;
using HR28.Application.Interfaces;
using HR28.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HR28.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly HR28DbContext _dbContext;
    private readonly IAccessScopeService _accessScopeService;
    private readonly IAuditTrailService _auditTrailService;

    public DashboardService(
        HR28DbContext dbContext,
        IAccessScopeService accessScopeService,
        IAuditTrailService auditTrailService)
    {
        _dbContext = dbContext;
        _accessScopeService = accessScopeService;
        _auditTrailService = auditTrailService;
    }

    public async Task<DashboardDto> GetDashboardAsync(Guid userId)
    {
        var scope = await _accessScopeService.GetAsync(userId);

        var voters = _dbContext.Voters.AsNoTracking().InScope(scope);
        var pledges = _dbContext.Pledges.AsNoTracking().InScope(scope, _dbContext.Voters);

        var statusCounts = await voters
            .GroupBy(v => v.SupportStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        int Status(string status) =>
            statusCounts.Where(x => x.Status == status).Sum(x => x.Count);

        var scopedConstituencyIds = scope.ConstituencyIds.ToList();
        var scopedIslandIds = scope.IslandIds.ToList();

        var islandCount = scope.IsAdministrator
            ? await _dbContext.Islands.CountAsync()
            : await _dbContext.Islands.CountAsync(i =>
                scopedConstituencyIds.Contains(i.ConstituencyId) ||
                i.ConstituencyIslands.Any(ci => scopedConstituencyIds.Contains(ci.ConstituencyId)) ||
                scopedIslandIds.Contains(i.Id));

        return new DashboardDto
        {
            IsAdministrator = scope.IsAdministrator,

            // Account figures are only meaningful to administrators.
            TotalUsers = scope.IsAdministrator ? await _dbContext.Users.CountAsync() : 0,
            TotalRoles = scope.IsAdministrator ? await _dbContext.Roles.CountAsync() : 0,

            TotalVoters = statusCounts.Sum(x => x.Count),
            Supporters = Status("Supporter"),
            Opponents = Status("Opponent"),
            Undecided = Status("Undecided"),
            Neutral = Status("Neutral"),

            TotalConstituencies = await _dbContext.Constituencies.InScope(scope).CountAsync(),
            TotalIslands = islandCount,

            TotalInfluencers = await _dbContext.Influencers.InScope(scope).CountAsync(),
            TotalEncounters = await _dbContext.Encounters.InScope(scope, _dbContext.Voters).CountAsync(),
            TotalPledges = await pledges.CountAsync(),
            OpenPledges = await pledges.CountAsync(x => x.Status == "Open"),
            CompletedPledges = await pledges.CountAsync(x => x.Status == "Completed")
        };
    }

    public async Task<List<RecentActivityDto>>
        GetRecentActivitiesAsync(
            Guid userId,
            int count = 20)
    {
        // Same visibility as the audit trail: administrators see all, others their own actions.
        var entries = await _auditTrailService.GetRecentAsync(userId, count);

        return entries
            .Select(x => new RecentActivityDto
            {
                UserId = x.ActorId,
                Action = x.Action,
                EntityName = x.EntityName,
                EntityId = x.EntityId,
                CreatedAt = x.CreatedAt,
                ActorName = x.ActorName,
                EntityLabel = x.EntityLabel
            })
            .ToList();
    }
}
