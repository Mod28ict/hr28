using HR28.Web.Filters;
using HR28.Web.Models;
using HR28.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace HR28.Web.Controllers;

[SessionAuthorize]
public class InfluencersController : AppController
{
    private readonly DashboardService _dashboardService;
    private readonly ApiClient _apiClient;

    public InfluencersController(
        DashboardService dashboardService,
        ApiClient apiClient)
    {
        _dashboardService = dashboardService;
        _apiClient = apiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new CreateInfluencerViewModel();

        var failure = await LoadConstituenciesAsync(model);

        return failure ?? View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateInfluencerViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return await LoadConstituenciesAsync(model) ?? View(model);
        }

        var result = await _apiClient.PostAsync<InfluencerDto>(
            "Influencers",
            model.Influencer,
            Token);

        if (!result.Success)
        {
            // Scope and duplicate errors belong on the form, not a redirect.
            if (result.IsUnauthorized)
                return HandleApiFailure(result)!;

            ModelState.AddModelError(string.Empty, result.Message);

            return await LoadConstituenciesAsync(model) ?? View(model);
        }

        TempData["SuccessMessage"] =
            $"{result.Data?.FullName ?? "The influencer"} was added.";

        return RedirectToAction(nameof(Index));
    }

    /// <summary>Islands of a constituency that the user may use (for the cascade).</summary>
    [HttpGet]
    public async Task<IActionResult> IslandsInScope(Guid constituencyId)
    {
        var result = await _apiClient.GetAsync<List<LookupDto>>(
            $"Constituencies/{constituencyId}/islands/in-scope",
            Token);

        if (!result.Success)
            return StatusCode((int)result.StatusCode);

        return Json(result.Data ?? new());
    }

    private async Task<IActionResult?> LoadConstituenciesAsync(CreateInfluencerViewModel model)
    {
        var result = await _apiClient.GetAsync<List<LookupDto>>(
            "Constituencies/in-scope",
            Token);

        if (!result.Success)
        {
            var redirect = HandleApiFailure(result);

            if (redirect != null)
                return redirect;

            ModelState.AddModelError(string.Empty, result.Message);
        }

        model.Constituencies = result.Data ?? new();

        return null;
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