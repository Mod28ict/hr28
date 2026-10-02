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
    private readonly ApiClient _apiClient;

    public DashboardController(
        DashboardService dashboardService,
        ApiClient apiClient)
    {
        _dashboardService = dashboardService;
        _apiClient = apiClient;
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

        await EnsureCampaignNameAsync(token);

        // Every list below is limited to the user's scope by the API.
        var activities = await _apiClient.GetAsync<List<RecentActivityDto>>(
            "Dashboard/recent-activity?count=5", token);

        var recentVoters = await _apiClient.GetAsync<List<VoterSearchDto>>(
            "Voters/recent?count=5", token);

        var constituencies = await _apiClient.GetAsync<List<ConstituencySummaryDto>>(
            "Reports/constituency-summary", token);

        var model =
            new DashboardViewModel
            {
                Dashboard = dashboard,
                Activities = activities.Data ?? new(),
                RecentVoters = recentVoters.Data ?? new(),
                Constituencies = constituencies.Data ?? new()
            };

        return View(model);
    }

    /// <summary>The sidebar shows the campaign name; load it once per session.</summary>
    private async Task EnsureCampaignNameAsync(string token)
    {
        if (!string.IsNullOrWhiteSpace(HttpContext.Session.GetString("CampaignName")))
            return;

        var settings = await _apiClient.GetAsync<SystemSettingsDto>("Settings/system", token);

        if (settings.Success && !string.IsNullOrWhiteSpace(settings.Data?.CampaignName))
            HttpContext.Session.SetString("CampaignName", settings.Data.CampaignName);
    }
}
