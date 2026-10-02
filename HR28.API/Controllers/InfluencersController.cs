using HR28.Application.DTOs.Influencers;
using HR28.Application.Interfaces;
using HR28.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HR28.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InfluencersController : ControllerBase
{
    private readonly IInfluencerService _influencerService;

    public InfluencersController(
        IInfluencerService influencerService)
    {
        _influencerService = influencerService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateInfluencerDto request)
    {
        var result = await _influencerService
            .CreateAsync(request);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        return Ok(await _influencerService.GetByIdAsync(id));
    }

    /// <summary>Needs the "Edit influencers" right and the influencer in the user's areas.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] CreateInfluencerDto request)
    {
        return Ok(await _influencerService.UpdateAsync(id, request));
    }

    /// <summary>Permanent. Needs the "Delete influencers" right and the influencer in the user's areas.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var name = await _influencerService.DeleteAsync(id);

        return Ok(new { message = $"{name} was deleted." });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _influencerService
            .GetAllAsync();

        return Ok(result);
    }

    [HttpPost("link")]
    public async Task<IActionResult> LinkToVoter(
        [FromBody] LinkInfluencerDto request)
    {
        await _influencerService
            .LinkToVoterAsync(request);

        return Ok(new
        {
            Message = "Influencer linked to voter successfully."
        });
    }
    [HttpGet("voter/{voterId:guid}")]
    public async Task<IActionResult> GetByVoter(
        Guid voterId)
    {
        var result = await _influencerService
            .GetByVoterIdAsync(voterId);

        return Ok(result);
    }
    [HttpPut("relationship")]
    public async Task<IActionResult>
        UpdateRelationship(
            UpdateInfluencerRelationshipDto request)
    {
        await _influencerService
            .UpdateRelationshipAsync(
                request);

        return NoContent();
    }


}