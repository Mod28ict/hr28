using HR28.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace HR28.Web.Controllers;

public class ReportsController : Controller
{
    private readonly DashboardService _dashboardService;

    public ReportsController(
        DashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    public async Task<IActionResult>
        ConstituencySummary()
    {
        var token =
            HttpContext.Session.GetString(
                "JwtToken");

        var data =
            await _dashboardService
                .GetConstituencySummaryAsync(token);

        return View(data);
    }
    public async Task<IActionResult>
        TopInfluencers()
    {
        var token =
            HttpContext.Session.GetString(
                "JwtToken");

        var result =
            await _dashboardService
                .GetTopInfluencersAsync(token);

        return View(result);
    }
    public async Task<IActionResult>
        Pledges()
    {
        var token =
            HttpContext.Session.GetString(
                "JwtToken");

        var data =
            await _dashboardService
                .GetPledgeSummaryAsync(token);

        return View(data);
    }

}