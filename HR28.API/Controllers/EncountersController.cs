using System.Security.Claims;
using HR28.Application.DTOs.Encounters;
using HR28.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

    [HttpGet("voter/{voterId}")]
    public async Task<IActionResult> GetByVoter(
        Guid voterId)
    {
        var result = await _encounterService
            .GetByVoterIdAsync(voterId);

        return Ok(result);
    }
}