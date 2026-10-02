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
