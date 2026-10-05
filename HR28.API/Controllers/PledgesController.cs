using HR28.API.Extensions;
using HR28.Application.Common;
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
    [RequirePermission(PermissionCatalog.PledgesView)]
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
    [RequirePermission(PermissionCatalog.PledgesAdd)]
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
    [RequirePermission(PermissionCatalog.PledgesView)]
    public async Task<IActionResult> GetByVoter(
        Guid voterId)
    {
        var result = await _pledgeService
            .GetByVoterIdAsync(voterId);

        return Ok(result);
    }
    [HttpGet("{pledgeId:guid}")]
    [RequirePermission(PermissionCatalog.PledgesView, PermissionCatalog.PledgesEdit)]
    public async Task<IActionResult> GetById(Guid pledgeId)
    {
        return Ok(await _pledgeService.GetByIdAsync(pledgeId));
    }

    [HttpPut("{pledgeId}/status")]
    [RequirePermission(PermissionCatalog.PledgesEdit)]
    public async Task<IActionResult> UpdateStatus(
    Guid pledgeId,
    [FromBody] UpdatePledgeStatusDto request)
    {
        var result = await _pledgeService
            .UpdateStatusAsync(pledgeId, request);

        return Ok(result);
    }

    /// <summary>Permanent; needs the "Delete pledges" right and the voter in the user's areas.</summary>
    [HttpDelete("{pledgeId:guid}")]
    [RequirePermission(PermissionCatalog.PledgesDelete)]
    public async Task<IActionResult> Delete(Guid pledgeId)
    {
        await _pledgeService.DeleteAsync(pledgeId);

        return Ok(new { message = "The pledge was deleted." });
    }
}