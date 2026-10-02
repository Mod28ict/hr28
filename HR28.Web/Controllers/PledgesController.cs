using HR28.Web.Filters;
using HR28.Web.Models;
using HR28.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace HR28.Web.Controllers;

[SessionAuthorize]
public class PledgesController : Controller
{
    private readonly DashboardService _dashboardService;

    public PledgesController(
        DashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet]
    public IActionResult Create(Guid voterId)
    {
        if (voterId == Guid.Empty)
        {
            return BadRequest();
        }

        var model = new CreatePledgeDto
        {
            VoterId = voterId,
            PledgeDate = DateTime.Now
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        CreatePledgeDto model)
    {
        var token =
            HttpContext.Session.GetString(
                "JwtToken");

        if (string.IsNullOrWhiteSpace(token))
        {
            return RedirectToAction(
                "Login",
                "Auth");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var success =
            await _dashboardService
                .CreatePledgeAsync(
                    model,
                    token);

        if (!success)
        {
            ModelState.AddModelError(
                string.Empty,
                string.IsNullOrWhiteSpace(_dashboardService.LastErrorMessage)
                    ? "The pledge could not be saved."
                    : _dashboardService.LastErrorMessage);

            return View(model);
        }

        TempData["SuccessMessage"] =
            "Pledge recorded successfully.";

        return RedirectToAction(
            "Profile",
            "Voters",
            new
            {
                id = model.VoterId
            });
    }
}