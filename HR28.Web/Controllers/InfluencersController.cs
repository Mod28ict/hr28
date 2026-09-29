using HR28.Web.Models;
using HR28.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace HR28.Web.Controllers;

public class InfluencersController : Controller
{
    private readonly DashboardService _dashboardService;

    public InfluencersController(
        DashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    public async Task<IActionResult> Index()
    {
        var token =
            HttpContext.Session.GetString(
                "JwtToken");

        var influencers =
            await _dashboardService
                .GetInfluencersAsync(token);

        return View(influencers ?? new());
    }

    [HttpGet]
    public async Task<IActionResult> Link(
        Guid voterId)
    {
        if (voterId == Guid.Empty)
        {
            return BadRequest();
        }

        var token =
            HttpContext.Session.GetString(
                "JwtToken");

        if (string.IsNullOrWhiteSpace(token))
        {
            return RedirectToAction(
                "Login",
                "Auth");
        }

        var influencers =
            await _dashboardService
                .GetInfluencersAsync(token)
            ?? new();

        var model =
            new LinkInfluencerViewModel
            {
                Link = new LinkInfluencerDto
                {
                    VoterId = voterId
                },

                Influencers = influencers
                    .OrderBy(x => x.FullName)
                    .ToList()
            };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Link(
        LinkInfluencerViewModel model)
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
            await ReloadInfluencersAsync(
                model,
                token);

            return View(model);
        }

        var success =
            await _dashboardService
                .LinkInfluencerAsync(
                    model.Link,
                    token);

        if (!success)
        {
            ModelState.AddModelError(
                string.Empty,
                "The influencer could not be linked. " +
                "The relationship may already exist.");

            await ReloadInfluencersAsync(
                model,
                token);

            return View(model);
        }

        TempData["SuccessMessage"] =
            "Influencer linked successfully.";

        return RedirectToAction(
            "Profile",
            "Voters",
            new
            {
                id = model.Link.VoterId
            });
    }

    private async Task ReloadInfluencersAsync(
        LinkInfluencerViewModel model,
        string token)
    {
        model.Influencers =
            await _dashboardService
                .GetInfluencersAsync(token)
            ?? new();

        model.Influencers =
            model.Influencers
                .OrderBy(x => x.FullName)
                .ToList();
    }
    [HttpGet]
    public IActionResult EditRelationship(
        Guid voterId,
        Guid influencerId)
    {
        var model =
            new UpdateInfluencerRelationshipDto
            {
                VoterId = voterId,
                InfluencerId = influencerId
            };

        return View(model);
    }
    [HttpPost]
    public async Task<IActionResult>
        EditRelationship(
            UpdateInfluencerRelationshipDto model)
    {
        var token =
            HttpContext.Session
                .GetString("JwtToken");

        var success =
            await _dashboardService
                .UpdateRelationshipAsync(
                    model,
                    token);

        if (!success)
        {
            return View(model);
        }

        return RedirectToAction(
            "Profile",
            "Voters",
            new
            {
                id = model.VoterId
            });
    }

}