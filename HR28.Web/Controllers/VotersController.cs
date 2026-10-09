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
        string? searchTerm = null,
        Guid? constituencyId = null,
        Guid? islandId = null,
        string? house = null,
        bool houseExact = false,
        string? status = null,
        string? party = null,
        string? gender = null,
        string? sort = null,
        bool desc = false)
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

        var parties = await LoadPartiesAsync(token);

        var filter = new VoterListFilterModel
        {
            SearchTerm = searchTerm?.Trim(),
            ConstituencyId = constituencyId,
            // An island only makes sense inside the chosen constituency.
            IslandId = constituencyId.HasValue ? islandId : null,
            House = house?.Trim(),
            HouseExact = houseExact,
            Status = VoterListFilterModel.Statuses.Contains(status) ? status : null,
            Gender = gender is "M" or "F" ? gender : null,
            // No party in the address: the list opens on the default party (MDP).
            Party = VoterListFilterModel.ResolveParty(party, parties),
            Parties = parties,
            PageSize = pageSize is 10 or 20 or 50 or 100 ? pageSize : 20,
            Sort = VoterListFilterModel.SortColumns.Contains(sort) && sort != "name" ? sort : null,
            Desc = desc
        };

        // Only constituencies and islands inside the user's areas are offered.
        var constituencies = await _apiClient.GetAsync<List<LookupDto>>(
            "Constituencies/in-scope", token);

        if (constituencies.IsUnauthorized)
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Auth");
        }

        filter.Constituencies = constituencies.Data ?? new();

        if (filter.ConstituencyId.HasValue)
        {
            var islands = await _apiClient.GetAsync<List<LookupDto>>(
                $"Constituencies/{filter.ConstituencyId}/islands/in-scope", token);

            filter.Islands = (islands.Data ?? new()).OrderBy(i => i.Name).ToList();
        }

        var result = await _apiClient.GetAsync<PagedResult<VoterSearchDto>>(
            $"Voters?page={Math.Max(1, page)}" + filter.ToApiQuery(),
            token);

        if (result.IsUnauthorized)
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Auth");
        }

        if (!result.Success)
            ViewBag.ErrorMessage = result.Message;

        ViewBag.Filter = filter;
        ViewBag.SearchTerm = filter.SearchTerm;

        // Remembered so "Add encounter only" roles come back to the same filtered page.
        HttpContext.Session.SetString(LastVoterListKey, Request.QueryString.Value ?? string.Empty);

        return View(result.Data ?? new PagedResult<VoterSearchDto>
        {
            Page = 1,
            PageSize = filter.PageSize
        });
    }

    /// <summary>Session key: the query string of the last Voters list shown (search, filters, page).</summary>
    public const string LastVoterListKey = "LastVoterListQuery";

    /// <summary>The Voters list address with the last search and filters, e.g. "/Voters?party=all&amp;page=3".</summary>
    public static string LastVoterListUrl(Microsoft.AspNetCore.Mvc.IUrlHelper url, ISession session)
    {
        var query = session.GetString(LastVoterListKey) ?? string.Empty;

        return (url.Action("Index", "Voters") ?? "/Voters") + (query.StartsWith('?') ? query : string.Empty);
    }

    /// <summary>Political parties for the filter and the voter form (empty if they can't be loaded).</summary>
    private async Task<List<PoliticalPartyDto>> LoadPartiesAsync(string? token)
    {
        var result = await _apiClient.GetAsync<List<PoliticalPartyDto>>("PoliticalParties", token);

        return result.Data ?? new();
    }

    /// <summary>Status pop-up on the voter list: changes only the support status.</summary>
    [HttpPost]
    public async Task<IActionResult> SetStatus(Guid id, string status, string? returnUrl)
    {
        var result = await _apiClient.PutAsync<object>(
            $"Voters/{id}/status",
            new { status },
            HttpContext.Session.GetString("JwtToken"));

        if (result.IsUnauthorized)
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Auth");
        }

        if (result.Success)
            TempData["SuccessMessage"] = $"Status changed to {status}.";
        else
            TempData["FlashError"] = string.IsNullOrWhiteSpace(result.Message)
                ? "The status could not be changed."
                : result.Message;

        // Back to the same filtered page (local addresses only).
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : RedirectToAction(nameof(Index));
    }

    /// <summary>Islands of a constituency inside the user's areas, for the island filter.</summary>
    [HttpGet]
    public async Task<IActionResult> IslandsInScope(Guid constituencyId)
    {
        var result = await _apiClient.GetAsync<List<LookupDto>>(
            $"Constituencies/{constituencyId}/islands/in-scope",
            HttpContext.Session.GetString("JwtToken"));

        if (!result.Success)
            return StatusCode((int)result.StatusCode);

        return Json((result.Data ?? new()).OrderBy(i => i.Name));
    }


    [HttpPost]
    public async Task<IActionResult>
        Search(string searchTerm)
    {
        var token =
            HttpContext.Session.GetString(
                "JwtToken");

        var voters =
            await _dashboardService
                .SearchVotersAsync(
                    searchTerm,
                    token);

        return View(
            "Index",
            voters ?? new());
    }
    public async Task<IActionResult>
        Profile(Guid id)
    {
        // Roles set to "Add encounter only" (Settings → Roles & rights): with only "Add
        // encounters" they go straight to that form; with more add rights (pledges, linking
        // influencers) they choose from cards. The API limits what the profile returns anyway.
        if (Hr28Permissions.OpensVotersOnAddEncounter(HttpContext.Session))
        {
            var choices = VoterQuickActions.For(HttpContext.Session);

            if (VoterQuickActions.OnlyEncounter(HttpContext.Session))
                return RedirectToAction("Create", "Encounters", new { voterId = id });

            if (choices.Count > 0)
            {
                var voter = await _apiClient.GetAsync<VoterSearchDto>($"Voters/{id}", HttpContext.Session.GetString("JwtToken"));

                if (voter.IsUnauthorized)
                {
                    HttpContext.Session.Clear();
                    return RedirectToAction("Login", "Auth");
                }

                if (!voter.Success || voter.Data == null)
                {
                    TempData["FlashError"] = "That voter could not be found.";
                    return LocalRedirect(LastVoterListUrl(Url, HttpContext.Session));
                }

                ViewBag.Choices = choices;
                return View("Choose", voter.Data);
            }
        }

        var token =
            HttpContext.Session.GetString(
                "JwtToken");

        var profile =
            await _dashboardService
                .GetVoterProfileAsync(
                    id,
                    token);

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
                        ?? new(),

                Parties = await LoadPartiesAsync(token)
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

        if (result.IsUnauthorized)
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Auth");
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
            model.Parties = await LoadPartiesAsync(token);

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
            return RedirectToAction("Index");
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
                    PoliticalPartyId = profile.Voter.PoliticalPartyId,
                    Gender = profile.Voter.Gender,
                    DateOfBirth = profile.Voter.DateOfBirth,
                    Remarks = profile.Voter.Remarks,
                    SupportStatus = profile.Voter.SupportStatus
                },

                Parties = await LoadPartiesAsync(token),

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
    public async Task<IActionResult> Edit(
        VoterCreateViewModel model)
    {
        var token =
            HttpContext.Session.GetString(
                "JwtToken");

        model.Voter.Remarks ??= string.Empty;
        model.Voter.MobileNumber ??= string.Empty;

        var result = await _apiClient.PutAsync<object>(
            $"Voters/{model.Voter.Id!.Value}",
            model.Voter,
            token);

        if (result.IsUnauthorized)
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Auth");
        }

        if (!result.Success)
        {
            // Show the API's message (duplicate ID, area rules, unknown party…) on the form,
            // with the drop-downs filled again.
            ModelState.AddModelError(
                string.Empty,
                string.IsNullOrWhiteSpace(result.Message)
                    ? "The voter could not be saved. Please try again."
                    : result.Message);

            ViewBag.IsSuperAdmin = Hr28Roles.IsAdministrator(
                HttpContext.Session.GetString("UserRole"));

            model.Constituencies = await _dashboardService.GetConstituenciesAsync(token) ?? new();
            model.Islands = model.Voter.ConstituencyId != Guid.Empty
                ? await _dashboardService.GetIslandsByConstituencyAsync(model.Voter.ConstituencyId, token) ?? new()
                : new();
            model.Parties = await LoadPartiesAsync(token);

            return View(model);
        }

        TempData["SuccessMessage"] = $"{model.Voter.FullName} was updated.";

        return RedirectToAction(
            "Profile",
            new { id = model.Voter.Id });
    }

    /// <summary>Permanent delete from the profile (the page asks first). Needs "Delete voters".</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _apiClient.DeleteAsync($"Voters/{id}", HttpContext.Session.GetString("JwtToken"));

        if (result.IsUnauthorized)
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Auth");
        }

        if (!result.Success)
        {
            TempData["FlashError"] = string.IsNullOrWhiteSpace(result.Message)
                ? "The voter could not be deleted."
                : result.Message;

            return RedirectToAction(nameof(Profile), new { id });
        }

        TempData["SuccessMessage"] = "The voter was deleted.";

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// The voter's photo, passed through from the API (which checks the right and the
    /// voter's area). Never cached by the browser.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Photo(Guid id)
    {
        var result = await _apiClient.GetFileAsync($"Voters/{id}/photo", HttpContext.Session.GetString("JwtToken"));

        if (!result.Success || result.Data == null)
            return NotFound();

        Response.Headers.CacheControl = "no-store, private";

        return File(result.Data.Content, result.Data.ContentType);
    }

    /// <summary>Adds or replaces the photo (JPG/PNG up to 2 MB). Needs "Add or remove voter photos".</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    // Larger than the 2 MB photo limit on purpose: a phone photo of a few MB reaches the
    // action and gets a plain "too large" message instead of an empty error page.
    [RequestSizeLimit(PhotoUploadReadLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = PhotoUploadReadLimit)]
    public async Task<IActionResult> UploadPhoto(Guid id, IFormFile? photo, string? returnUrl)
    {
        if (photo == null || photo.Length == 0)
        {
            TempData["FlashError"] = "Please choose a photo first.";
            return BackFromPhoto(id, returnUrl);
        }

        if (photo.Length > 2 * 1024 * 1024)
        {
            TempData["FlashError"] = $"The photo is too large ({photo.Length / (1024.0 * 1024.0):0.#} MB). Please choose one under 2 MB.";
            return BackFromPhoto(id, returnUrl);
        }

        var result = await _apiClient.PostFileAsync<object>($"Voters/{id}/photo", photo, HttpContext.Session.GetString("JwtToken"));

        if (result.IsUnauthorized)
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Auth");
        }

        if (result.Success)
            TempData["SuccessMessage"] = "Photo saved.";
        else
            TempData["FlashError"] = string.IsNullOrWhiteSpace(result.Message)
                ? "The photo could not be saved. Please try again."
                : result.Message;

        return BackFromPhoto(id, returnUrl);
    }

    private const long PhotoUploadReadLimit = 20 * 1024 * 1024;

    /// <summary>After a photo change: back to the page it was made on (e.g. Quick entry), else the profile.</summary>
    private IActionResult BackFromPhoto(Guid id, string? returnUrl) =>
        Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl!) : RedirectToAction(nameof(Profile), new { id });

    /// <summary>Removes the photo (the page asks first).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemovePhoto(Guid id, string? returnUrl)
    {
        var result = await _apiClient.DeleteAsync($"Voters/{id}/photo", HttpContext.Session.GetString("JwtToken"));

        if (result.IsUnauthorized)
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Auth");
        }

        if (result.Success)
            TempData["SuccessMessage"] = "Photo removed.";
        else
            TempData["FlashError"] = string.IsNullOrWhiteSpace(result.Message)
                ? "The photo could not be removed. Please try again."
                : result.Message;

        return BackFromPhoto(id, returnUrl);
    }
}
