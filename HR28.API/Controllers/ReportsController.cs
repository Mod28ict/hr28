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
}