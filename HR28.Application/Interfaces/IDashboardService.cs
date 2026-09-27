using HR28.Application.DTOs.Dashboard;

namespace HR28.Application.Interfaces;

public interface IDashboardService
{
    Task<DashboardDto> GetDashboardAsync(Guid userId);
}