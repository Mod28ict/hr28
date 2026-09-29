using HR28.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

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

        return View(dashboard);

    }
}