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
        var result =
            await _reportService
                .GetConstituencySummaryAsync();

        return Ok(result);
    }
    [HttpGet("pledge-status-summary")]
    public async Task<IActionResult>
    GetPledgeStatusSummary()
    {
        var result =
            await _reportService
                .GetPledgeStatusSummaryAsync();

        return Ok(result);
    }
    [HttpGet("top-influencers")]
    public async Task<IActionResult>
        GetTopInfluencers()
    {
        var result =
            await _reportService
                .GetTopInfluencersAsync();

        return Ok(result);
    }
}