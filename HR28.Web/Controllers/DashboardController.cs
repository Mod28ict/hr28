using HR28.Web.Models;
using HR28.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HR28.Web.Controllers;

public class DashboardController : Controller
{
    private readonly DashboardService _dashboardService;

    public DashboardController(
        DashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    public async Task<IActionResult> Index()
    {
        var token =
            HttpContext.Session.GetString("JwtToken");

        var dashboard =
            await _dashboardService.GetDashboardAsync(token);

        var activities =
            await _dashboardService
                .GetRecentActivityAsync(token);

        var recentVoters =
            await _dashboardService
                .GetRecentVotersAsync(token);
        var model =
            new DashboardViewModel
            {
                Dashboard = dashboard,
                Activities = activities ?? new(),
                RecentVoters = recentVoters ?? new()
            };

        return View(model);

    }
}