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

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        if (!Hr28Permissions.Has(HttpContext.Session, Hr28Permissions.InfluencersEdit))
        {
            TempData["FlashError"] = "You don't have permission to edit influencers. Ask your Administrator.";
            return RedirectToAction(nameof(Index));
        }

        var existing = await _apiClient.GetAsync<InfluencerDto>($"Influencers/{id}", Token);

        if (HandleApiFailure(existing) is { } redirect)
            return redirect;

        if (!existing.Success || existing.Data == null)
        {
            TempData["FlashError"] = "That influencer could not be found.";
            return RedirectToAction(nameof(Index));
        }

        var model = new CreateInfluencerViewModel
        {
            Id = id,
            Influencer = new CreateInfluencerDto
            {
                NationalId = existing.Data.NationalId,
                FullName = existing.Data.FullName,
                Address = existing.Data.Address,
                ContactNumber = existing.Data.ContactNumber,
                ConstituencyId = existing.Data.ConstituencyId,
                IslandId = existing.Data.IslandId,
                CategoryId = existing.Data.CategoryId,
                Remarks = existing.Data.Remarks
            }
        };

        return await LoadConstituenciesAsync(model) ?? View("Create", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, CreateInfluencerViewModel model)
    {
        model.Id = id;

        if (!ModelState.IsValid)
            return await LoadConstituenciesAsync(model) ?? View("Create", model);

        var result = await _apiClient.PutAsync<InfluencerDto>(
            $"Influencers/{id}",
            model.Influencer,
            Token);

        if (!result.Success)
        {
            if (result.IsUnauthorized)
                return HandleApiFailure(result)!;

            // No right, duplicate ID, or area outside the user's scope: show it on the form.
            ModelState.AddModelError(string.Empty, result.Message);

            return await LoadConstituenciesAsync(model) ?? View("Create", model);
        }

        TempData["SuccessMessage"] = $"{model.Influencer.FullName} was updated.";

        return RedirectToAction(nameof(Index));
    }

    /// <summary>Permanent delete (the page asks for confirmation first).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _apiClient.DeleteAsync($"Influencers/{id}", Token);

        if (result.IsUnauthorized)
            return HandleApiFailure(result)!;

        if (result.Success)
            TempData["SuccessMessage"] = "The influencer was deleted.";
        else
            TempData["FlashError"] = result.Message;

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Voters linked to an influencer. Influencers are global; the list shows only voters
    /// inside the user's areas and counts the rest.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Voters(
        Guid id,
        int page = 1,
        int pageSize = 20,
        string? searchTerm = null,
        Guid? constituencyId = null,
        string? status = null,
        string? relationship = null)
    {
        var filter = new LinkedVoterFilterModel
        {
            SearchTerm = searchTerm?.Trim(),
            ConstituencyId = constituencyId,
            Status = VoterListFilterModel.Statuses.Contains(status) ? status : null,
            Relationship = string.IsNullOrWhiteSpace(relationship) ? null : relationship.Trim(),
            PageSize = pageSize is 10 or 20 or 50 or 100 ? pageSize : 20
        };

        var result = await _apiClient.GetAsync<InfluencerVotersDto>(
            $"Influencers/{id}/voters?page={Math.Max(1, page)}" + filter.ToApiQuery(),
            Token);

        if (HandleApiFailure(result) is { } redirect)
            return redirect;

        if (!result.Success || result.Data == null)
        {
            TempData["FlashError"] = string.IsNullOrWhiteSpace(result.Message)
                ? "That influencer could not be found."
                : result.Message;
            return RedirectToAction(nameof(Index));
        }

        var constituencies = await _apiClient.GetAsync<List<LookupDto>>("Constituencies/in-scope", Token);
        filter.Constituencies = constituencies.Data ?? new();

        ViewBag.Filter = filter;

        return View(result.Data);
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

        var categories = await _apiClient.GetAsync<List<InfluencerCategoryDto>>("InfluencerCategories", Token);
        model.Categories = categories.Data ?? new();

        return null;
    }

    /// <summary>
    /// The Influencers table: same search and filters as Voters, plus category.
    /// Searched and paged by the API. Influencers are global (not area-limited).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(
        int page = 1,
        int pageSize = 20,
        string? searchTerm = null,
        Guid? constituencyId = null,
        Guid? islandId = null,
        string? category = null)
    {
        var filter = new InfluencerListFilterModel
        {
            SearchTerm = searchTerm?.Trim(),
            ConstituencyId = constituencyId,
            // An island only makes sense inside the chosen constituency.
            IslandId = constituencyId.HasValue ? islandId : null,
            NoCategory = category == InfluencerListFilterModel.NoCategoryValue,
            CategoryId = Guid.TryParse(category, out var categoryId) ? categoryId : null,
            PageSize = pageSize is 10 or 20 or 50 or 100 ? pageSize : 20
        };

        var result = await _apiClient.GetAsync<PagedResult<InfluencerDto>>(
            $"Influencers/search?page={Math.Max(1, page)}" + filter.ToApiQuery(),
            Token);

        if (HandleApiFailure(result) is { } redirect)
            return redirect;

        if (!result.Success)
            ViewBag.ErrorMessage = result.Message;

        var constituencies = await _apiClient.GetAsync<List<LookupDto>>("Constituencies", Token);
        filter.Constituencies = (constituencies.Data ?? new()).OrderBy(c => c.Name).ToList();

        if (filter.ConstituencyId.HasValue)
        {
            var islands = await _apiClient.GetAsync<List<LookupDto>>(
                $"Constituencies/{filter.ConstituencyId}/islands", Token);

            filter.Islands = (islands.Data ?? new()).OrderBy(i => i.Name).ToList();
        }

        var categories = await _apiClient.GetAsync<List<InfluencerCategoryDto>>("InfluencerCategories", Token);
        filter.Categories = categories.Data ?? new();

        ViewBag.Filter = filter;

        return View(result.Data ?? new PagedResult<InfluencerDto>
        {
            Page = 1,
            PageSize = filter.PageSize
        });
    }

    /// <summary>All islands of a constituency, for the list filter (influencers are global).</summary>
    [HttpGet]
    public async Task<IActionResult> Islands(Guid constituencyId)
    {
        var result = await _apiClient.GetAsync<List<LookupDto>>(
            $"Constituencies/{constituencyId}/islands",
            Token);

        if (!result.Success)
            return StatusCode((int)result.StatusCode);

        return Json((result.Data ?? new()).OrderBy(i => i.Name));
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

        var result =
            await _apiClient.PostAsync<object>(
                "Influencers/link",
                model.Link,
                token);

        if (!result.Success)
        {
            if (result.IsUnauthorized)
                return HandleApiFailure(result)!;

            // e.g. "Mariyam Ahmed is already linked to this voter (as Child). ..."
            ModelState.AddModelError(
                string.Empty,
                string.IsNullOrWhiteSpace(result.Message)
                    ? "The influencer could not be linked. Please try again."
                    : result.Message);

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