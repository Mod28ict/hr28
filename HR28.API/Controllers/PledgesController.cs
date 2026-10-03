using HR28.API.Extensions;
using System.Security.Claims;
using HR28.Application.DTOs.Pledges;
using HR28.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HR28.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PledgesController : ControllerBase
{
    private readonly IPledgeService _pledgeService;

    public PledgesController(
        IPledgeService pledgeService)
    {
        _pledgeService = pledgeService;
    }

    /// <summary>All pledges inside the user's areas (paged, filterable). Rate-limited like voter search.</summary>
    [HttpGet]
    [EnableRateLimiting(RateLimitPolicies.Search)]
    public async Task<IActionResult> GetList(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? status = null,
        [FromQuery] bool overdue = false)
    {
        return Ok(await _pledgeService.GetListAsync(
            page,
            pageSize,
            new PledgeListFilter
            {
                SearchTerm = searchTerm,
                Status = status,
                OverdueOnly = overdue
            }));
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.RecordWriter)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePledgeDto request)
    {
        var userId = Guid.Parse(
            User.FindFirst(ClaimTypes.NameIdentifier)!
                .Value);

        var result = await _pledgeService
            .CreateAsync(userId, request);

        return Ok(result);
    }

    [HttpGet("voter/{voterId}")]
    public async Task<IActionResult> GetByVoter(
        Guid voterId)
    {
        var result = await _pledgeService
            .GetByVoterIdAsync(voterId);

        return Ok(result);
    }
    [HttpGet("{pledgeId:guid}")]
    public async Task<IActionResult> GetById(Guid pledgeId)
    {
        return Ok(await _pledgeService.GetByIdAsync(pledgeId));
    }

    [HttpPut("{pledgeId}/status")]
    [Authorize(Policy = AuthorizationPolicies.RecordWriter)]
    public async Task<IActionResult> UpdateStatus(
    Guid pledgeId,
    [FromBody] UpdatePledgeStatusDto request)
    {
        var result = await _pledgeService
            .UpdateStatusAsync(pledgeId, request);

        return Ok(result);
    }
}