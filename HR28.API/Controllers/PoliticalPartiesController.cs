using HR28.API.Extensions;
using HR28.Application.DTOs.Voters;
using HR28.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HR28.API.Controllers;

/// <summary>
/// The managed list of political parties. Everyone signed in can read it (voter form,
/// party filter); only administrators can change it.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PoliticalPartiesController : ControllerBase
{
    private readonly IPoliticalPartyService _service;

    public PoliticalPartiesController(IPoliticalPartyService service)
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
    public async Task<IActionResult> Create([FromBody] SavePoliticalPartyDto request)
    {
        return Ok(await _service.CreateAsync(request));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Administrator)]
    public async Task<IActionResult> Update(Guid id, [FromBody] SavePoliticalPartyDto request)
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

    /// <summary>The party the Voters list opens on; no id = all parties.</summary>
    [HttpPut("default-filter")]
    [Authorize(Policy = AuthorizationPolicies.Administrator)]
    public async Task<IActionResult> SetDefaultFilter([FromQuery] Guid? id)
    {
        await _service.SetDefaultFilterAsync(id);

        return NoContent();
    }
}
