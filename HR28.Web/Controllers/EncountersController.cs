using HR28.Web.Filters;
using HR28.Web.Models;
using HR28.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace HR28.Web.Controllers;

[SessionAuthorize]
public class EncountersController : Controller
{
    private readonly DashboardService _dashboardService;

    public EncountersController(
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

        var model = new CreateEncounterDto
        {
            VoterId = voterId,
            EncounterDate = DateTime.Now
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        CreateEncounterDto model)
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
                .CreateEncounterAsync(
                    model,
                    token);

        if (!success)
        {
            ModelState.AddModelError(
                string.Empty,
                string.IsNullOrWhiteSpace(_dashboardService.LastErrorMessage)
                    ? "The encounter could not be saved."
                    : _dashboardService.LastErrorMessage);

            return View(model);
        }

        TempData["SuccessMessage"] =
            "Encounter recorded successfully.";

        return RedirectToAction(
            "Profile",
            "Voters",
            new
            {
                id = model.VoterId
            });
    }
}