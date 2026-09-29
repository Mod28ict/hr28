using HR28.Web.Models;
using HR28.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace HR28.Web.Controllers;

public class VotersController : Controller
{
    private readonly DashboardService _dashboardService;

    public VotersController(
        DashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    public IActionResult Index()
    {
        return View(
            new List<VoterSearchDto>());
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
                        ?? new()
            };

        return View(model);
    }
    [HttpPost]
    public async Task<IActionResult>
        Create(VoterCreateViewModel model)
    {
        var token =
            HttpContext.Session.GetString(
                "JwtToken");

        var success =
            await _dashboardService
                .CreateVoterAsync(
                    model.Voter,
                    token);

        if (!success)
            return View(model);

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

        return View(model);
    }
    [HttpPost]
    public async Task<IActionResult>
        Edit(VoterCreateViewModel model)
    {
        var token =
            HttpContext.Session.GetString(
                "JwtToken");

        var success =
            await _dashboardService
                .UpdateVoterAsync(
                    model.Voter.Id!.Value,
                    model.Voter,
                    token);

        if (!success)
            return View(model);

        return RedirectToAction(
            "Profile",
            new { id = model.Voter.Id });
    }
}