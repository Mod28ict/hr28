using HR28.API.Extensions;
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
    [Authorize(Policy = AuthorizationPolicies.RecordWriter)]
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

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        return Ok(await _encounterService.GetByIdAsync(id));
    }

    /// <summary>Needs the granted right "Edit encounters" (checked in the service with the voter's area).</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateEncounterDto request)
    {
        return Ok(await _encounterService.UpdateAsync(id, request));
    }
}
