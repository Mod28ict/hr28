using HR28.API.Extensions;
using HR28.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HR28.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IReportingService _reportService;

    public ReportsController(
        IReportingService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("constituency-summary")]
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
}
