using HR28.Web.Models;
using HR28.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using HR28.Web.Filters;


namespace HR28.Web.Controllers;

[SessionAuthorize]
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
            HttpContext.Session.GetString(
                "JwtToken");

        if (string.IsNullOrWhiteSpace(token))
        {
            HttpContext.Session.Clear();

            return RedirectToAction(
                "Login",
                "Auth");
        }

        var dashboard =
            await _dashboardService
                .GetDashboardAsync(token);

        if (dashboard == null)
        {
            HttpContext.Session.Clear();

            return RedirectToAction(
                "Login",
                "Auth");
        }

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
                RecentVoters = recentVoters ?? new(),
                Constituencies =
                    await GetConstituenciesForRoleAsync(token)
            };

        return View(model);
    }

    private async Task<List<ConstituencySummaryDto>>
        GetConstituenciesForRoleAsync(string token)
    {
        var role =
            HttpContext.Session.GetString(
                "UserRole");

        var isNational =
            role == "Super Administrator"
            || role == "National Administrator"
            || role == "Reporter";

        if (!isNational)
        {
            return new();
        }

        try
        {
            return await _dashboardService
                .GetConstituencySummaryAsync(token)
                ?? new();
        }
        catch (HttpRequestException)
        {
            // The panel is optional; show its empty state rather than failing the dashboard.
            return new();
        }
    }
}