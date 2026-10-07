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
        [FromQuery] bool noParty = false,
        [FromQuery] string? gender = null)
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
                    NoParty = noParty,
                    Gender = gender
                });

        return Ok(result);
    }


    /// <summary>
    /// Quick entry: a voter by ID card (National ID), only inside the user's areas.
    /// Rate-limited like search so it can't be used to sweep the registry.
    /// </summary>
    [HttpGet("by-national-id/{nationalId}")]
    [RequirePermission(PermissionCatalog.VotersView)]
    [EnableRateLimiting(RateLimitPolicies.Search)]
    public async Task<IActionResult> GetByNationalId(string nationalId)
    {
        if (User.GetUserId() is not Guid userId)
            return Unauthorized();

        var voter = await _voterService.GetByNationalIdAsync(userId, nationalId);

        return voter == null
            ? NotFound(new { message = "No voter with that ID card was found in your areas." })
            : Ok(voter);
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
    [RequirePermission(PermissionCatalog.VotersStatus)]
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
    /// <summary>
    /// The voter's photo. Needs "View voter photos" and the voter in the user's areas;
    /// rate-limited like search so photos can't be bulk-downloaded, and never cached.
    /// </summary>
    [HttpGet("{id:guid}/photo")]
    [RequirePermission(PermissionCatalog.VotersPhotoView)]
    [EnableRateLimiting(RateLimitPolicies.Search)]
    public async Task<IActionResult> GetPhoto(Guid id, [FromServices] IVoterPhotoService photos)
    {
        var photo = await photos.GetAsync(id);

        if (photo == null)
            return NotFound(new { message = "This voter has no photo." });

        Response.Headers.CacheControl = "no-store, private";

        return File(photo.Value.Content, photo.Value.ContentType);
    }

    /// <summary>Adds or replaces the photo (JPG/PNG up to 2 MB; metadata is removed). Audited.</summary>
    [HttpPost("{id:guid}/photo")]
    [RequirePermission(PermissionCatalog.VotersPhotoEdit)]
    [EnableRateLimiting(RateLimitPolicies.PhotoChange)]
    [RequestSizeLimit(VoterPhotoMaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = VoterPhotoMaxRequestBytes)]
    public async Task<IActionResult> UploadPhoto(Guid id, IFormFile? file, [FromServices] IVoterPhotoService photos)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Please choose a photo." });

        if (file.Length > HR28.Infrastructure.Services.VoterPhotoService.MaxBytes)
            return BadRequest(new { message = "The photo is too large. Please choose one under 2 MB." });

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer);

        await photos.SaveAsync(id, buffer.ToArray());

        return Ok(new { message = "Photo saved." });
    }

    /// <summary>Removes the photo. Audited.</summary>
    [HttpDelete("{id:guid}/photo")]
    [RequirePermission(PermissionCatalog.VotersPhotoEdit)]
    [EnableRateLimiting(RateLimitPolicies.PhotoChange)]
    public async Task<IActionResult> RemovePhoto(Guid id, [FromServices] IVoterPhotoService photos)
    {
        await photos.RemoveAsync(id);

        return Ok(new { message = "Photo removed." });
    }

    private const long VoterPhotoMaxRequestBytes = 2 * 1024 * 1024 + 64 * 1024;

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