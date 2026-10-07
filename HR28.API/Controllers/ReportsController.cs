using HR28.Application.Common;
using HR28.API.Extensions;
using HR28.Application.Interfaces;
using HR28.Infrastructure.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HR28.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IReportingService _reportService;
    private readonly IAccessScopeService _accessScopeService;
    private readonly IAuditService _auditService;

    public ReportsController(
        IReportingService reportService,
        IAccessScopeService accessScopeService,
        IAuditService auditService)
    {
        _reportService = reportService;
        _accessScopeService = accessScopeService;
        _auditService = auditService;
    }

    [HttpGet("constituency-summary")]
    [RequirePermission(PermissionCatalog.ReportsView)]
    public async Task<IActionResult>
        GetConstituencySummary()
    {
        var userId = User.GetUserId();

        if (userId == null)
            return Unauthorized();

        return Ok(await _reportService
            .GetConstituencySummaryAsync(userId.Value));
    }

    [HttpGet("pledge-status-summary")]
    [RequirePermission(PermissionCatalog.ReportsView)]
    public async Task<IActionResult>
        GetPledgeStatusSummary()
    {
        var userId = User.GetUserId();

        if (userId == null)
            return Unauthorized();

        return Ok(await _reportService
            .GetPledgeStatusSummaryAsync(userId.Value));
    }

    [HttpGet("top-influencers")]
    [RequirePermission(PermissionCatalog.ReportsView)]
    public async Task<IActionResult>
        GetTopInfluencers([FromQuery] int top = 10)
    {
        var userId = User.GetUserId();

        if (userId == null)
            return Unauthorized();

        top = Math.Clamp(top, 1, 50);

        return Ok(await _reportService
            .GetTopInfluencersAsync(userId.Value, top));
    }

    // --------------------------------------------------
    // CSV downloads: same scope as the on-screen reports,
    // and every download is recorded in the audit trail.
    // --------------------------------------------------

    [HttpGet("constituency-summary/export")]
    [RequirePermission(PermissionCatalog.ReportsDownload)]
    [EnableRateLimiting(RateLimitPolicies.Export)]
    public async Task<IActionResult> ExportConstituencySummary()
    {
        var userId = User.GetUserId();

        if (userId == null)
            return Unauthorized();

        var rows = await _reportService.GetConstituencySummaryAsync(userId.Value);

        var csv = await StartCsvAsync(userId.Value, "Constituency Summary");

        csv.Row("Constituency", "Code", "Total voters", "Supporters", "Undecided", "Neutral",
                "Opposition", "Support %", "Influencers", "Encounters", "Pledges");

        foreach (var r in rows)
        {
            csv.Row(r.ConstituencyName, r.ConstituencyCode, r.TotalVoters, r.Supporters, r.Undecided, r.Neutral,
                    r.Opponents, r.SupportPercentage, r.TotalInfluencers, r.TotalEncounters, r.TotalPledges);
        }

        csv.Row("Total",
            rows.Sum(r => r.TotalVoters),
            rows.Sum(r => r.Supporters),
            rows.Sum(r => r.Undecided),
            rows.Sum(r => r.Neutral),
            rows.Sum(r => r.Opponents),
            rows.Sum(r => r.TotalVoters) == 0
                ? 0m
                : Math.Round(rows.Sum(r => r.Supporters) * 100m / rows.Sum(r => r.TotalVoters), 2),
            rows.Sum(r => r.TotalInfluencers),
            rows.Sum(r => r.TotalEncounters),
            rows.Sum(r => r.TotalPledges));

        return await FileAsync(userId.Value, csv, "Constituency Summary", "constituency-summary");
    }

    [HttpGet("pledge-status-summary/export")]
    [RequirePermission(PermissionCatalog.ReportsDownload)]
    [EnableRateLimiting(RateLimitPolicies.Export)]
    public async Task<IActionResult> ExportPledgeStatusSummary()
    {
        var userId = User.GetUserId();

        if (userId == null)
            return Unauthorized();

        var s = await _reportService.GetPledgeStatusSummaryAsync(userId.Value);
        var total = s.Pending + s.InProgress + s.Completed + s.Cancelled;

        decimal Share(int value) => total == 0 ? 0 : Math.Round(value * 100m / total, 2);

        var csv = await StartCsvAsync(userId.Value, "Pledge Summary");

        csv.Row("Status", "Pledges", "Share %")
           .Row("Open", s.Pending, Share(s.Pending))
           .Row("In progress", s.InProgress, Share(s.InProgress))
           .Row("Completed", s.Completed, Share(s.Completed))
           .Row("Cancelled", s.Cancelled, Share(s.Cancelled))
           .Row("Total", total, total == 0 ? 0m : 100m);

        return await FileAsync(userId.Value, csv, "Pledge Summary", "pledge-summary");
    }

    [HttpGet("top-influencers/export")]
    [RequirePermission(PermissionCatalog.ReportsDownload)]
    [EnableRateLimiting(RateLimitPolicies.Export)]
    public async Task<IActionResult> ExportTopInfluencers([FromQuery] int top = 50)
    {
        var userId = User.GetUserId();

        if (userId == null)
            return Unauthorized();

        var rows = await _reportService.GetTopInfluencersAsync(userId.Value, Math.Clamp(top, 1, 50));

        var csv = await StartCsvAsync(userId.Value, "Top Influencers");

        csv.Row("Rank", "Influencer", "Linked voters");

        var rank = 0;
        foreach (var r in rows)
            csv.Row(++rank, r.FullName, r.LinkedVoters);

        return await FileAsync(userId.Value, csv, "Top Influencers", "top-influencers");
    }

    /// <summary>Title block so a downloaded file says what it is and whose data it holds.</summary>
    private async Task<CsvBuilder> StartCsvAsync(Guid userId, string title)
    {
        var scope = await _accessScopeService.GetAsync(userId);

        return new CsvBuilder()
            .Row($"HR28 — {title}")
            .Row("Scope", scope.IsAdministrator ? "All constituencies" : "Assigned area only")
            .Row("Generated (Maldives time)", MaldivesTime.Now.ToString("dd MMM yyyy HH:mm"))
            .Blank();
    }

    private async Task<IActionResult> FileAsync(Guid userId, CsvBuilder csv, string title, string slug)
    {
        await _auditService.LogAsync(userId, "Export CSV", "Report", title);

        var fileName = $"hr28-{slug}-{MaldivesTime.Now:yyyy-MM-dd}.csv";

        return File(csv.ToBytes(), "text/csv; charset=utf-8", fileName);
    }
}
