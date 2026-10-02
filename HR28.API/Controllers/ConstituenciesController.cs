using HR28.API.Extensions;
using HR28.Application.DTOs;
using HR28.Application.DTOs.Constituencies;
using HR28.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HR28.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConstituenciesController : ControllerBase
{
    private readonly IConstituencyService _service;
    private readonly IAccessScopeService _accessScopeService;

    public ConstituenciesController(
        IConstituencyService service,
        IAccessScopeService accessScopeService)
    {
        _service = service;
        _accessScopeService = accessScopeService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await _service.GetAllAsync());
    }

    /// <summary>Constituencies the signed-in user may create records in.</summary>
    [HttpGet("in-scope")]
    public async Task<IActionResult> GetInScope()
    {
        var userId = User.GetUserId();

        if (userId == null)
            return Unauthorized();

        var scope = await _accessScopeService.GetAsync(userId.Value);
        var all = await _service.GetAllAsync();

        var visible = all
            .Where(c => scope.IsAdministrator || scope.VisibleConstituencyIds.Contains(c.Id))
            .OrderBy(c => c.Name)
            .Select(c => new LookupDto { Id = c.Id, Name = c.Name });

        return Ok(visible);
    }

    /// <summary>Islands of a constituency that the signed-in user may use.</summary>
    [HttpGet("{id}/islands/in-scope")]
    public async Task<IActionResult> GetIslandsInScope(Guid id)
    {
        var userId = User.GetUserId();

        if (userId == null)
            return Unauthorized();

        var scope = await _accessScopeService.GetAsync(userId.Value);
        var islands = await _service.GetIslandsByConstituencyAsync(id);

        if (scope.IsAdministrator || scope.ConstituencyIds.Contains(id))
            return Ok(islands);

        return Ok(islands.Where(i => scope.IslandIds.Contains(i.Id)));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var item = await _service.GetByIdAsync(id);

        if (item == null)
            return NotFound();

        return Ok(item);
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.Administrator)]
    public async Task<IActionResult> Create(
        CreateConstituencyDto dto)
    {
        var result = await _service.CreateAsync(dto);

        return Ok(result);
    }

    [HttpPut("{id}")]
    [Authorize(Policy = AuthorizationPolicies.Administrator)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateConstituencyDto dto)
    {
        var success =
            await _service.UpdateAsync(id, dto);

        if (!success)
            return NotFound();

        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = AuthorizationPolicies.Administrator)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var success =
            await _service.DeleteAsync(id);

        if (!success)
            return NotFound();

        return NoContent();
    }

    [HttpGet("{id}/islands")]
    public async Task<IActionResult>
        GetIslands(Guid id)
    {
        var islands =
            await _service
                .GetIslandsByConstituencyAsync(id);

        return Ok(islands);
    }
}
