using HR28.API.Extensions;
using HR28.Application.DTOs.Influencers;
using HR28.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HR28.API.Controllers;

/// <summary>
/// The managed list of influencer categories. Everyone signed in can read it (for the
/// form and the filter); only administrators can change it.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InfluencerCategoriesController : ControllerBase
{
    private readonly IInfluencerCategoryService _service;

    public InfluencerCategoriesController(IInfluencerCategoryService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await _service.GetAllAsync());
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.Administrator)]
    public async Task<IActionResult> Create([FromBody] SaveInfluencerCategoryDto request)
    {
        return Ok(await _service.CreateAsync(request));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Administrator)]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveInfluencerCategoryDto request)
    {
        return Ok(await _service.UpdateAsync(id, request));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Administrator)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var name = await _service.DeleteAsync(id);

        return Ok(new { message = $"{name} was deleted." });
    }
}
