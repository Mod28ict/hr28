using HR28.Web.Models;
using HR28.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace HR28.Web.Controllers;

/// <summary>
/// Campaign reports. The API limits every report to the user's scope;
/// administrators get national figures.
/// </summary>
public class ReportsController : AppController
{
    private readonly ApiClient _apiClient;

    public ReportsController(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    /// <summary>Reports need "View reports"; downloads also need "Download and print reports". The API checks both.</summary>
    public override void OnActionExecuting(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context)
    {
        var session = HttpContext.Session;
        var download = string.Equals(context.ActionDescriptor.RouteValues["action"], nameof(Download), StringComparison.OrdinalIgnoreCase);

        if (!string.IsNullOrEmpty(Token) &&
            (!Hr28Permissions.Has(session, Hr28Permissions.ReportsView) ||
             (download && !Hr28Permissions.Has(session, Hr28Permissions.ReportsDownload))))
        {
            TempData["FlashError"] = download
                ? "Downloading reports needs the \"Download and print reports\" right. Ask your Administrator."
                : "Reports need the \"View reports\" right. Ask your Administrator.";
            context.Result = RedirectToAction("Index", "Dashboard");
            return;
        }

        base.OnActionExecuting(context);
    }

    public async Task<IActionResult> Index()
    {
        var summary = await _apiClient.GetAsync<List<ConstituencySummaryDto>>(
            "Reports/constituency-summary", Token);

        if (HandleApiFailure(summary) is { } redirect)
            return redirect;

        var pledges = await _apiClient.GetAsync<PledgeStatusSummaryDto>(
            "Reports/pledge-status-summary", Token);

        var influencers = await _apiClient.GetAsync<List<TopInfluencerDto>>(
            "Reports/top-influencers?top=5", Token);

        ViewBag.IsAdministrator = IsAdministrator;
        ViewBag.Pledges = pledges.Data ?? new PledgeStatusSummaryDto();
        ViewBag.TopInfluencers = influencers.Data ?? new List<TopInfluencerDto>();

        return View(summary.Data ?? new List<ConstituencySummaryDto>());
    }

    public async Task<IActionResult> ConstituencySummary()
    {
        var result = await _apiClient.GetAsync<List<ConstituencySummaryDto>>(
            "Reports/constituency-summary", Token);

        if (HandleApiFailure(result) is { } redirect)
            return redirect;

        ViewBag.IsAdministrator = IsAdministrator;

        return View(result.Data ?? new List<ConstituencySummaryDto>());
    }

    public async Task<IActionResult> TopInfluencers()
    {
        var result = await _apiClient.GetAsync<List<TopInfluencerDto>>(
            "Reports/top-influencers?top=25", Token);

        if (HandleApiFailure(result) is { } redirect)
            return redirect;

        ViewBag.IsAdministrator = IsAdministrator;

        return View(result.Data ?? new List<TopInfluencerDto>());
    }

    // Report key → API export path. Only these reports can be downloaded.
    private static readonly Dictionary<string, (string Path, string ReturnAction)> Exports = new()
    {
        ["constituencies"] = ("Reports/constituency-summary/export", nameof(ConstituencySummary)),
        ["pledges"] = ("Reports/pledge-status-summary/export", nameof(Pledges)),
        ["influencers"] = ("Reports/top-influencers/export?top=50", nameof(TopInfluencers))
    };

    /// <summary>Downloads a report as CSV. Scope and auditing are handled by the API.</summary>
    public async Task<IActionResult> Download(string report)
    {
        if (!Exports.TryGetValue(report ?? string.Empty, out var export))
            return RedirectToAction(nameof(Index));

        var result = await _apiClient.GetFileAsync(export.Path, Token);

        if (HandleApiFailure(result) is { } redirect)
            return redirect;

        if (!result.Success || result.Data == null)
        {
            TempData["FlashError"] = "The report could not be downloaded. Please try again.";
            return RedirectToAction(export.ReturnAction);
        }

        return File(result.Data.Content, result.Data.ContentType, result.Data.FileName);
    }

    public async Task<IActionResult> Pledges()
    {
        var result = await _apiClient.GetAsync<PledgeStatusSummaryDto>(
            "Reports/pledge-status-summary", Token);

        if (HandleApiFailure(result) is { } redirect)
            return redirect;

        ViewBag.IsAdministrator = IsAdministrator;

        return View(result.Data ?? new PledgeStatusSummaryDto());
    }
}
