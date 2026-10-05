using HR28.API.Extensions;
using HR28.Application.Common;
using HR28.Application.DTOs.Voters;
using HR28.Application.Interfaces;
using HR28.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace HR28.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class VotersController : ControllerBase
{
    private readonly IVoterService _voterService;

    public VotersController(IVoterService voterService)
    {
        _voterService = voterService;
    }

    [HttpPost]
    [RequirePermission(PermissionCatalog.VotersAdd)]
    public async Task<IActionResult> CreateVoter(
        CreateVoterDto request)
    {
        var result =
            await _voterService.CreateVoterAsync(request);

        return Ok(result);
    }

    [HttpGet]
    [RequirePermission(PermissionCatalog.VotersView)]
    [EnableRateLimiting(RateLimitPolicies.Search)]
    public async Task<IActionResult> GetVoters(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? searchTerm = null,
        [FromQuery] Guid? constituencyId = null,
        [FromQuery] Guid? islandId = null,
        [FromQuery] string? house = null,
        [FromQuery] bool houseExact = false,
        [FromQuery] string? status = null,
        [FromQuery] Guid? partyId = null,
        [FromQuery] bool noParty = false)
    {
        var userIdValue =
            User.FindFirst(
                ClaimTypes.NameIdentifier)?
                .Value;

        if (!Guid.TryParse(
            userIdValue,
            out var userId))
        {
            return Unauthorized();
        }

        var result =
            await _voterService.GetVotersAsync(
                userId,
                page,
                pageSize,
                new VoterListFilter
                {
                    SearchTerm = searchTerm,
                    ConstituencyId = constituencyId,
                    IslandId = islandId,
                    House = house,
                    HouseExact = houseExact,
                    Status = status,
                    PartyId = partyId,
                    NoParty = noParty
                });

        return Ok(result);
    }


    [HttpGet("{id}")]
    [RequirePermission(PermissionCatalog.VotersView)]
    public async Task<IActionResult> GetVoter(Guid id)
    {
        var userIdClaim =
            User.FindFirst(
                ClaimTypes.NameIdentifier);

        if (userIdClaim == null ||
            !Guid.TryParse(
                userIdClaim.Value,
                out var userId))
        {
            return Unauthorized();
        }
        var result =
await _voterService.GetVoterByIdAsync(
    userId,
    id);

        if (result == null)
            return NotFound();

        return Ok(result);
    }
    [HttpGet("search")]
    [RequirePermission(PermissionCatalog.VotersView)]
    [EnableRateLimiting(RateLimitPolicies.Search)]
    public async Task<IActionResult> Search(
        [FromQuery] string searchTerm)
    {
        var userIdClaim = User.FindFirst(
            ClaimTypes.NameIdentifier);

        if (userIdClaim == null ||
            !Guid.TryParse(
                userIdClaim.Value,
                out var userId))
        {
            return Unauthorized();
        }

        var result =
            await _voterService.SearchVotersAsync(
                userId,
                searchTerm);

        return Ok(result);
    }
    [HttpPut("{id}")]
    [RequirePermission(PermissionCatalog.VotersEdit)]
    public async Task<IActionResult> UpdateVoter(
        Guid id,
        UpdateVoterDto request)
    {
        await _voterService.UpdateVoterAsync(
            id,
            request);

        return Ok("Voter updated successfully.");
    }


    public class UpdateStatusRequest
    {
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>Changes only the support status (status pop-up). Audited.</summary>
    [HttpPut("{id:guid}/status")]
    [RequirePermission(PermissionCatalog.VotersEdit)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusRequest request)
    {
        var status = await _voterService.UpdateStatusAsync(id, request.Status);

        return Ok(new { status });
    }

    /// <summary>Permanent; needs the "Delete voters" right and the voter in the user's areas.</summary>
    [HttpDelete("{id}")]
    [RequirePermission(PermissionCatalog.VotersDelete)]
    public async Task<IActionResult> DeleteVoter(Guid id)
    {
        await _voterService.DeleteVoterAsync(id);

        return Ok("Voter deleted successfully.");
    }
    /// <summary>
    /// Live duplicate check for the voter forms. Rate-limited like search so it
    /// can't be used to sweep the registry; reveals details only for voters in scope.
    /// </summary>
    [HttpGet("national-id-check")]
    [RequirePermission(PermissionCatalog.VotersAdd, PermissionCatalog.VotersEdit)]
    [EnableRateLimiting(RateLimitPolicies.Search)]
    public async Task<IActionResult> CheckNationalId(
        [FromQuery] string nationalId,
        [FromQuery] Guid? excludeId = null)
    {
        if (User.GetUserId() is not Guid userId)
            return Unauthorized();

        return Ok(await _voterService.CheckNationalIdAsync(userId, nationalId, excludeId));
    }

    /// <summary>The profile; sections the user has no view right for come back empty.</summary>
    [HttpGet("{id}/profile")]
    [RequirePermission(PermissionCatalog.VotersView)]
    public async Task<IActionResult> GetProfile(
        Guid id)
    {
        var userIdValue =
            User.FindFirst(
                ClaimTypes.NameIdentifier)?
                .Value;

        if (!Guid.TryParse(
            userIdValue,
            out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var profile =
                await _voterService
                    .GetProfileAsync(
                        userId,
                        id);

            return Ok(profile);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
    [HttpGet("recent")]
    [RequirePermission(PermissionCatalog.VotersView)]
    public async Task<IActionResult> GetRecent(
        [FromQuery] int count = 10)
    {
        var userIdClaim =
            User.FindFirst(
                ClaimTypes.NameIdentifier);

        if (userIdClaim == null ||
            !Guid.TryParse(
                userIdClaim.Value,
                out var userId))
        {
            return Unauthorized();
        }

        var result =
            await _voterService
                .GetRecentAsync(userId, Math.Clamp(count, 1, 50));

        return Ok(result);
    }
}