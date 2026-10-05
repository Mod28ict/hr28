using HR28.Web.Filters;
using HR28.Web.Models;
using HR28.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace HR28.Web.Controllers;

public class EncountersController : AppController
{
    private readonly DashboardService _dashboardService;
    private readonly ApiClient _apiClient;

    public EncountersController(
        DashboardService dashboardService,
        ApiClient apiClient)
    {
        _dashboardService = dashboardService;
        _apiClient = apiClient;
    }

    /// <summary>All encounters inside the user's areas, with who they were with and the outcome.</summary>
    [HttpGet]
    public async Task<IActionResult> Index(
        int page = 1,
        int pageSize = 20,
        string? searchTerm = null,
        string? type = null,
        string? outcome = null,
        string? response = null)
    {
        var filter = new EncounterListFilterModel
        {
            SearchTerm = searchTerm?.Trim(),
            Type = EncounterListFilterModel.Types.Contains(type) ? type : null,
            Outcome = EncounterListFilterModel.Outcomes.Contains(outcome) ? outcome : null,
            Response = EncounterListFilterModel.Responses.Any(r => r.Value == response) ? response : null,
            PageSize = pageSize is 10 or 20 or 50 or 100 ? pageSize : 20
        };

        var result = await _apiClient.GetAsync<PagedResult<EncounterListItemDto>>(
            $"Encounters?page={Math.Max(1, page)}" + filter.ToApiQuery(),
            Token);

        if (HandleApiFailure(result) is { } redirect)
            return redirect;

        if (!result.Success)
            ViewBag.ErrorMessage = result.Message;

        ViewBag.Filter = filter;

        return View(result.Data ?? new PagedResult<EncounterListItemDto> { Page = 1, PageSize = filter.PageSize });
    }

    private const string NoEditRight =
        "You don't have permission to edit encounters. Ask your Administrator.";

    /// <summary>Edit an encounter. Needs the "Edit encounters" right; the API checks it again with the voter's area.</summary>
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        if (!Hr28Permissions.Has(HttpContext.Session, Hr28Permissions.EncountersEdit))
        {
            TempData["FlashError"] = NoEditRight;
            return RedirectToAction("Index", "Dashboard");
        }

        var existing = await _apiClient.GetAsync<EncounterDto>($"Encounters/{id}", Token);

        if (HandleApiFailure(existing) is { } redirect)
            return redirect;

        if (!existing.Success || existing.Data == null)
        {
            TempData["FlashError"] = "That encounter could not be found.";
            return RedirectToAction("Index", "Voters");
        }

        return View("Create", new CreateEncounterDto
        {
            EncounterId = id,
            VoterId = existing.Data.VoterId,
            EncounterDate = existing.Data.EncounterDate,
            EncounterType = existing.Data.EncounterType,
            Outcome = existing.Data.Outcome,
            Response = existing.Data.Response,
            Notes = existing.Data.Notes
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(Guid id, CreateEncounterDto model)
    {
        model.EncounterId = id;

        if (!ModelState.IsValid)
            return View("Create", model);

        var result = await _apiClient.PutAsync<EncounterDto>(
            $"Encounters/{id}",
            new
            {
                model.EncounterDate,
                model.EncounterType,
                model.Outcome,
                model.Response,
                Notes = model.Notes ?? string.Empty
            },
            Token);

        if (!result.Success)
        {
            if (result.IsUnauthorized)
                return HandleApiFailure(result)!;

            // No right, outside the user's areas, or a validation message: show it on the form.
            ModelState.AddModelError(string.Empty, result.Message);
            return View("Create", model);
        }

        TempData["SuccessMessage"] = "Encounter updated.";

        return RedirectToAction("Profile", "Voters", new { id = result.Data?.VoterId ?? model.VoterId });
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
            EncounterDate = Hr28Time.Now
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        CreateEncounterDto model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _apiClient.PostAsync<EncounterDto>(
            "Encounters",
            new
            {
                model.VoterId,
                model.EncounterDate,
                model.EncounterType,
                model.Outcome,
                model.Response,
                Notes = model.Notes ?? string.Empty
            },
            Token);

        if (!result.Success)
        {
            if (result.IsUnauthorized)
                return HandleApiFailure(result)!;

            // A validation message from the API (or "outside your areas"): show it on the form.
            ModelState.AddModelError(
                string.Empty,
                string.IsNullOrWhiteSpace(result.Message)
                    ? "The encounter could not be saved. Please try again."
                    : result.Message);

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