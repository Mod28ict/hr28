using HR28.Web.Filters;
using HR28.Web.Models;
using HR28.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace HR28.Web.Controllers;

[SessionAuthorize]
public class PledgesController : Controller
{
    private readonly DashboardService _dashboardService;
    private readonly ApiClient _apiClient;

    public PledgesController(
        DashboardService dashboardService,
        ApiClient apiClient)
    {
        _dashboardService = dashboardService;
        _apiClient = apiClient;
    }

    /// <summary>All pledges inside the user's areas, with who they are for and their state.</summary>
    [HttpGet]
    public async Task<IActionResult> Index(
        int page = 1,
        int pageSize = 20,
        string? searchTerm = null,
        string? status = null,
        bool overdue = false)
    {
        var filter = new PledgeListFilterModel
        {
            SearchTerm = searchTerm?.Trim(),
            Status = UpdatePledgeViewModel.Statuses.Any(s => s.Value == status) ? status : null,
            Overdue = overdue,
            PageSize = pageSize is 10 or 20 or 50 or 100 ? pageSize : 20
        };

        var result = await _apiClient.GetAsync<PagedResult<PledgeListItemDto>>(
            $"Pledges?page={Math.Max(1, page)}" + filter.ToApiQuery(),
            HttpContext.Session.GetString("JwtToken"));

        if (result.IsUnauthorized)
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Auth");
        }

        if (!result.Success)
            ViewBag.ErrorMessage = result.Message;

        ViewBag.Filter = filter;

        return View(result.Data ?? new PagedResult<PledgeListItemDto> { Page = 1, PageSize = filter.PageSize });
    }

    /// <summary>Change a pledge's status and record what was done.</summary>
    [HttpGet]
    public async Task<IActionResult> Update(Guid id)
    {
        var pledge = await _apiClient.GetAsync<PledgeDto>(
            $"Pledges/{id}",
            HttpContext.Session.GetString("JwtToken"));

        if (pledge.IsUnauthorized)
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Auth");
        }

        if (!pledge.Success || pledge.Data == null)
        {
            TempData["FlashError"] = "That pledge could not be found.";
            return RedirectToAction("Index", "Voters");
        }

        return View(new UpdatePledgeViewModel
        {
            Pledge = pledge.Data,
            Status = pledge.Data.Status,
            ResolutionNotes = pledge.Data.ResolutionNotes
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(Guid id, UpdatePledgeViewModel model)
    {
        var token = HttpContext.Session.GetString("JwtToken");

        var result = await _apiClient.PutAsync<PledgeDto>(
            $"Pledges/{id}/status",
            new { status = model.Status, resolutionNotes = model.ResolutionNotes ?? string.Empty },
            token);

        if (result.IsUnauthorized)
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Auth");
        }

        if (result.Success && result.Data != null)
        {
            TempData["SuccessMessage"] = $"Pledge \"{result.Data.Title}\" is now {result.Data.Status}.";
            return RedirectToAction("Profile", "Voters", new { id = result.Data.VoterId });
        }

        // Reload the pledge so the page can show it again with the message.
        var pledge = await _apiClient.GetAsync<PledgeDto>($"Pledges/{id}", token);

        if (pledge.Data == null)
        {
            TempData["FlashError"] = result.Message;
            return RedirectToAction("Index", "Voters");
        }

        ModelState.AddModelError(string.Empty, result.Message);
        model.Pledge = pledge.Data;

        return View(model);
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
            PledgeDate = Hr28Time.Now
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
                "The pledge could not be saved.");

            return View(model);
        }

        TempData["SuccessMessage"] =
            "Pledge recorded successfully.";

        // "Add encounter only" roles go back to the same filtered voter list.
        if (Hr28Permissions.OpensVotersOnAddEncounter(HttpContext.Session))
            return LocalRedirect(VotersController.LastVoterListUrl(Url, HttpContext.Session));

        return RedirectToAction(
            "Profile",
            "Voters",
            new
            {
                id = model.VoterId
            });
    }

    /// <summary>Permanent delete from the voter's profile (the page asks first). Needs "Delete pledges".</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, Guid voterId)
    {
        var result = await _apiClient.DeleteAsync($"Pledges/{id}", HttpContext.Session.GetString("JwtToken"));

        if (result.IsUnauthorized)
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Auth");
        }

        if (result.Success)
            TempData["SuccessMessage"] = "The pledge was deleted.";
        else
            TempData["FlashError"] = string.IsNullOrWhiteSpace(result.Message)
                ? "The pledge could not be deleted."
                : result.Message;

        return RedirectToAction("Profile", "Voters", new { id = voterId });
    }
}
