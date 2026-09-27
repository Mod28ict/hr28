using HR28.Application.DTOs.Dashboard;
using HR28.Application.Interfaces;
using HR28.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HR28.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly HR28DbContext _dbContext;

    public DashboardService(HR28DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DashboardDto> GetDashboardAsync()
    {
        return new DashboardDto
        {
            TotalUsers = await _dbContext.Users.CountAsync(),
            TotalVoters = await _dbContext.Voters.CountAsync(),
            TotalRoles = await _dbContext.Roles.CountAsync(),
            TotalConstituencies = await _dbContext.Constituencies.CountAsync(),
            TotalIslands = await _dbContext.Islands.CountAsync()
        };
    }
}