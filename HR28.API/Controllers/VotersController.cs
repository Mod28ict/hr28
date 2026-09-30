using HR28.Application.DTOs.Voters;
using HR28.Application.Interfaces;
using HR28.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    public async Task<IActionResult> CreateVoter(
        CreateVoterDto request)
    {
        var result =
            await _voterService.CreateVoterAsync(request);

        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetVoters(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? searchTerm = null)
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
                searchTerm);

        return Ok(result);
    }


    [HttpGet("{id}")]
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
    //[Authorize]
    [HttpGet("search")]
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
    public async Task<IActionResult> UpdateVoter(
        Guid id,
        UpdateVoterDto request)
    {
        await _voterService.UpdateVoterAsync(
            id,
            request);

        return Ok("Voter updated successfully.");
    }


    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteVoter(Guid id)
    {
        await _voterService.DeleteVoterAsync(id);

        return Ok("Voter deleted successfully.");
    }
    [HttpGet("{id}/profile")]
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
                .GetRecentAsync(count);

        return Ok(result);
    }
}