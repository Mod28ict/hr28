using HR28.Web.Filters;
using HR28.Web.Models;
using HR28.Web.Services;
using Microsoft.AspNetCore.Mvc;


namespace HR28.Web.Controllers;

[SessionAuthorize]
public class VotersController : Controller
{
    private readonly DashboardService _dashboardService;
    private readonly ApiClient _apiClient;

    public VotersController(
        DashboardService dashboardService,
        ApiClient apiClient)
    {
        _dashboardService = dashboardService;
        _apiClient = apiClient;
    }

    /// <summary>
    /// Live duplicate check used by the Add/Edit voter forms while typing.
    /// Returns { exists, voterId?, fullName? }; details only for voters in the user's areas.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> CheckNationalId(string nationalId, Guid? excludeId)
    {
        if (string.IsNullOrWhiteSpace(nationalId))
            return Json(new { exists = false });

        var query = "Voters/national-id-check?nationalId=" + Uri.EscapeDataString(nationalId.Trim());

        if (excludeId.HasValue)
            query += "&excludeId=" + excludeId.Value;

        var result = await _apiClient.GetAsync<NationalIdCheckResult>(
            query,
            HttpContext.Session.GetString("JwtToken"));

        if (!result.Success || result.Data == null)
            return StatusCode((int)result.StatusCode, new { message = result.Message });

        return Json(new
        {
            exists = result.Data.Exists,
            voterId = result.Data.VoterId,
            fullName = result.Data.FullName,
            profileUrl = result.Data.VoterId.HasValue
                ? Url.Action(nameof(Profile), new { id = result.Data.VoterId })
                : null
        });
    }

    private class NationalIdCheckResult
    {
        public bool Exists { get; set; }

        public Guid? VoterId { get; set; }

        public string? FullName { get; set; }
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        int page = 1,
        int pageSize = 20,
        string? searchTerm = null)
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

        var result =
            await _dashboardService.GetVotersAsync(
                token,
                page,
                pageSize,
                searchTerm);

        if (result == null)
        {
            result =
                new PagedResult<VoterSearchDto>
                {
                    Page = 1,
                    PageSize = pageSize
                };
        }

        ViewBag.SearchTerm = searchTerm;

        return View(result);
    }


    /// <summary>Old search form target; the voter list now searches with paging.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Search(string? searchTerm) =>
        RedirectToAction(nameof(Index), new { searchTerm });
    public async Task<IActionResult>
        Profile(Guid id)
    {
        var token =
            HttpContext.Session.GetString(
                "JwtToken");

        var profile =
            await _dashboardService
                .GetVoterProfileAsync(
                    id,
                    token);

        if (profile?.Voter == null)
        {
            TempData["FlashError"] = "That voter was not found, or is outside your areas.";
            return RedirectToAction(nameof(Index));
        }

        return View(profile);
    }
    public async Task<IActionResult> Create()
    {
        var token =
            HttpContext.Session
                .GetString("JwtToken");

        var model =
            new VoterCreateViewModel
            {
                Constituencies =
                    await _dashboardService
                        .GetConstituenciesAsync(token)
                        ?? new(),

                Islands =
                    await _dashboardService
                        .GetIslandsAsync(token)
                        ?? new()
            };

        return View(model);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult>
        Create(VoterCreateViewModel model)
    {
        var token =
            HttpContext.Session.GetString(
                "JwtToken");

        var result = await _apiClient.PostAsync<object>(
            "Voters",
            model.Voter,
            token);

        if (result.IsUnauthorized || result.IsForbidden ||
            result.StatusCode is System.Net.HttpStatusCode.TooManyRequests
                or System.Net.HttpStatusCode.ServiceUnavailable
            || (int)result.StatusCode >= 500)
        {
            throw new ApiCallException(result.StatusCode, result.Message);
        }

        if (!result.Success)
        {
            // e.g. "A voter with National ID A123456 already exists."
            ModelState.AddModelError(string.Empty, result.Message);

            // Reload the lists, or the constituency dropdown comes back empty.
            model.Constituencies =
                await _dashboardService.GetConstituenciesAsync(token) ?? new();
            model.Islands =
                await _dashboardService.GetIslandsAsync(token) ?? new();

            return View(model);
        }

        TempData["SuccessMessage"] = $"{model.Voter.FullName} was added.";

        return RedirectToAction(
            "Index");
    }
    [HttpGet]
    public async Task<IActionResult>
        GetIslands(Guid constituencyId)
    {
        var token =
            HttpContext.Session
                .GetString("JwtToken");

        var islands =
            await _dashboardService
                .GetIslandsByConstituencyAsync(
                    constituencyId,
                    token);

        return Json(islands);
    }
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var token =
            HttpContext.Session.GetString(
                "JwtToken");

        var profile =
            await _dashboardService
                .GetVoterProfileAsync(
                    id,
                    token);

        if (profile?.Voter == null)
        {
            TempData["FlashError"] = "That voter was not found, or is outside your areas.";
            return RedirectToAction(nameof(Index));
        }

        var model =
            new VoterCreateViewModel
            {
                Voter = new VoterCreateEditDto
                {
                    Id = profile.Voter.Id,
                    NationalId = profile.Voter.NationalId,
                    FullName = profile.Voter.FullName,
                    Address = profile.Voter.Address,
                    MobileNumber = profile.Voter.MobileNumber,
                    ConstituencyId = profile.Voter.ConstituencyId ?? Guid.Empty,
                    IslandId = profile.Voter.IslandId,
                    Remarks = profile.Voter.Remarks,
                    SupportStatus = profile.Voter.SupportStatus
                },

                Constituencies =
                    await _dashboardService
                        .GetConstituenciesAsync(token)
                        ?? new(),
                Islands =
                    profile.Voter.ConstituencyId.HasValue
                    && profile.Voter.ConstituencyId.Value != Guid.Empty
                        ? await _dashboardService
                            .GetIslandsByConstituencyAsync(
                                profile.Voter.ConstituencyId.Value,
                                token)
                            ?? new()
                        : new(),

            };

        // Only administrators may move a voter to another area (the API enforces this too).
        ViewBag.IsSuperAdmin = Hr28Roles.IsAdministrator(
            HttpContext.Session.GetString("UserRole"));
        return View(model);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        VoterCreateViewModel model)
    {
        var token =
            HttpContext.Session.GetString(
                "JwtToken");

        if (model.Voter.Id is null || model.Voter.Id == Guid.Empty)
            return RedirectToAction(nameof(Index));

        model.Voter.Remarks ??= string.Empty;
        model.Voter.MobileNumber ??= string.Empty;

        var success =
            await _dashboardService
                .UpdateVoterAsync(
                    model.Voter.Id!.Value,
                    model.Voter,
                    token);

        if (!success)
        {
            // e.g. "A voter with National ID A123456 already exists."
            ModelState.AddModelError(string.Empty, _dashboardService.LastErrorMessage);

            // Reload the lists, or the dropdowns come back empty.
            model.Constituencies =
                await _dashboardService.GetConstituenciesAsync(token) ?? new();
            model.Islands =
                model.Voter.ConstituencyId != Guid.Empty
                    ? await _dashboardService.GetIslandsByConstituencyAsync(
                          model.Voter.ConstituencyId, token) ?? new()
                    : new();

            ViewBag.IsSuperAdmin = Hr28Roles.IsAdministrator(
                HttpContext.Session.GetString("UserRole"));

            return View(model);
        }

        TempData["SuccessMessage"] = $"{model.Voter.FullName} was updated.";

        return RedirectToAction(
            "Profile",
            new { id = model.Voter.Id });
    }
}