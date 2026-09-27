using HR28.Application.DTOs.Dashboard;
using HR28.Application.Interfaces;
using HR28.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HR28.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly HR28DbContext _dbContext;
    private async Task<bool> HasFullAccessAsync(Guid userId)
    {
        return await _dbContext.UserRoles
            .Include(ur => ur.Role)
            .AnyAsync(ur =>
                ur.UserId == userId &&
                (
                    ur.Role.Name == "Super Administrator" ||
                    ur.Role.Name == "National Administrator"
                ));
    }


    public DashboardService(HR28DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DashboardDto> GetDashboardAsync(Guid userId)
    {
        var userScopes = await _dbContext.UserScopes.Where(x => x.UserId == userId).ToListAsync();
        var hasFullAccess = await HasFullAccessAsync(userId);
        var votersQuery = _dbContext.Voters.AsQueryable();

        if (!hasFullAccess)
        {
            if (!userScopes.Any())
            {
                votersQuery = votersQuery.Where(v => false);
            }
            else
            {
                var constituencyIds = userScopes
                .Where(x => x.ConstituencyId.HasValue)
                .Select(x => x.ConstituencyId!.Value)
                .ToList();

                var islandIds = userScopes
                    .Where(x => x.IslandId.HasValue)
                    .Select(x => x.IslandId!.Value)
                    .ToList();

                votersQuery = votersQuery.Where(v =>
                    constituencyIds.Contains(v.ConstituencyId) ||
                    (v.IslandId.HasValue &&
                     islandIds.Contains(v.IslandId.Value)));
            }
        }
        return new DashboardDto
        {
            TotalUsers = await _dbContext.Users.CountAsync(),
            TotalVoters = await votersQuery.CountAsync(),
            TotalRoles = await _dbContext.Roles.CountAsync(),
            TotalConstituencies = await _dbContext.Constituencies.CountAsync(),
            TotalIslands = await _dbContext.Islands.CountAsync(),
            Supporters = await votersQuery.CountAsync(v => v.SupportStatus == "Supporter"),
            Opponents = await votersQuery.CountAsync(v => v.SupportStatus == "Opponent"),
            Undecided = await votersQuery.CountAsync(v => v.SupportStatus == "Undecided"),
            Neutral = await votersQuery.CountAsync(v => v.SupportStatus == "Neutral")


        };
    }
}