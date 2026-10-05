using HR28.API.Extensions;
using HR28.Application.Common;
using HR28.Application.DTOs.Influencers;
using HR28.Application.Interfaces;
using HR28.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

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
    [RequirePermission(PermissionCatalog.InfluencersAdd)]
    public async Task<IActionResult> Create(
        [FromBody] CreateInfluencerDto request)
    {
        var result = await _influencerService
            .CreateAsync(request);

        return Ok(result);
    }

    /// <summary>
    /// The Influencers list: search (name, National ID, phone, island), constituency,
    /// island and category filters, paged. Influencers are global, so not area-limited.
    /// </summary>
    [HttpGet("search")]
    [RequirePermission(PermissionCatalog.InfluencersView)]
    [EnableRateLimiting(RateLimitPolicies.Search)]
    public async Task<IActionResult> Search(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? searchTerm = null,
        [FromQuery] Guid? constituencyId = null,
        [FromQuery] Guid? islandId = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] bool noCategory = false)
    {
        return Ok(await _influencerService.SearchAsync(
            page,
            pageSize,
            new InfluencerListFilter
            {
                SearchTerm = searchTerm,
                ConstituencyId = constituencyId,
                IslandId = islandId,
                CategoryId = categoryId,
                NoCategory = noCategory
            }));
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCatalog.InfluencersView, PermissionCatalog.InfluencersEdit)]
    public async Task<IActionResult> GetById(Guid id)
    {
        return Ok(await _influencerService.GetByIdAsync(id));
    }

    /// <summary>Voters linked to this influencer that the user may see (paged, filterable).</summary>
    [HttpGet("{id:guid}/voters")]
    [RequirePermission(PermissionCatalog.InfluencersView)]
    [EnableRateLimiting(RateLimitPolicies.Search)]
    public async Task<IActionResult> GetLinkedVoters(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? searchTerm = null,
        [FromQuery] Guid? constituencyId = null,
        [FromQuery] string? status = null,
        [FromQuery] string? relationship = null)
    {
        return Ok(await _influencerService.GetLinkedVotersAsync(
            id,
            page,
            pageSize,
            new LinkedVoterFilter
            {
                SearchTerm = searchTerm,
                ConstituencyId = constituencyId,
                Status = status,
                Relationship = relationship
            }));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCatalog.InfluencersEdit)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] CreateInfluencerDto request)
    {
        return Ok(await _influencerService.UpdateAsync(id, request));
    }

    /// <summary>Permanent. Needs the "Delete influencers" right and the influencer in the user's areas.</summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission(PermissionCatalog.InfluencersDelete)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var name = await _influencerService.DeleteAsync(id);

        return Ok(new { message = $"{name} was deleted." });
    }

    /// <summary>Every influencer by name (the "link to voter" picker).</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalog.InfluencersView, PermissionCatalog.InfluencersLink)]
    public async Task<IActionResult> GetAll()
    {
        var result = await _influencerService
            .GetAllAsync();

        return Ok(result);
    }

    [HttpPost("link")]
    [RequirePermission(PermissionCatalog.InfluencersLink)]
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
    [RequirePermission(PermissionCatalog.InfluencersView)]
    public async Task<IActionResult> GetByVoter(
        Guid voterId)
    {
        var result = await _influencerService
            .GetByVoterIdAsync(voterId);

        return Ok(result);
    }
    [HttpPut("relationship")]
    [RequirePermission(PermissionCatalog.InfluencersLink)]
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