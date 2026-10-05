using HR28.API.Extensions;
using HR28.Application.Common;
using System.Security.Claims;
using HR28.Application.DTOs.Encounters;
using HR28.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HR28.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EncountersController : ControllerBase
{
    private readonly IEncounterService _encounterService;

    public EncountersController(
        IEncounterService encounterService)
    {
        _encounterService = encounterService;
    }

    [HttpPost]
    [RequirePermission(PermissionCatalog.EncountersAdd)]
    public async Task<IActionResult> Create(
        [FromBody] CreateEncounterDto request)
    {
        var userId = Guid.Parse(
            User.FindFirst(ClaimTypes.NameIdentifier)!
                .Value);

        var result = await _encounterService
            .CreateAsync(userId, request);

        return Ok(result);
    }

    /// <summary>All encounters inside the user's areas (paged, filterable). Rate-limited like voter search.</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalog.EncountersView)]
    [EnableRateLimiting(RateLimitPolicies.Search)]
    public async Task<IActionResult> GetList(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? type = null,
        [FromQuery] string? outcome = null,
        [FromQuery] string? response = null)
    {
        return Ok(await _encounterService.GetListAsync(
            page,
            pageSize,
            new EncounterListFilter
            {
                SearchTerm = searchTerm,
                EncounterType = type,
                Outcome = outcome,
                Response = response
            }));
    }

    [HttpGet("voter/{voterId}")]
    [RequirePermission(PermissionCatalog.EncountersView)]
    public async Task<IActionResult> GetByVoter(
        Guid voterId)
    {
        var result = await _encounterService
            .GetByVoterIdAsync(voterId);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCatalog.EncountersView, PermissionCatalog.EncountersEdit)]
    public async Task<IActionResult> GetById(Guid id)
    {
        return Ok(await _encounterService.GetByIdAsync(id));
    }

    /// <summary>Needs the granted right "Edit encounters" (checked in the service with the voter's area).</summary>
    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCatalog.EncountersEdit)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateEncounterDto request)
    {
        return Ok(await _encounterService.UpdateAsync(id, request));
    }

    /// <summary>Permanent; needs the "Delete encounters" right and the voter in the user's areas.</summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission(PermissionCatalog.EncountersDelete)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _encounterService.DeleteAsync(id);

        return Ok(new { message = "The encounter was deleted." });
    }
}
