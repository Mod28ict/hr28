using HR28.API.Extensions;
using HR28.Application.DTOs.Users;
using HR28.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HR28.API.Controllers;

// Account management is limited to Super Administrators (matches the web app's Users menu).
[Authorize(Policy = AuthorizationPolicies.SuperAdministrator)]
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser(
        CreateUserDto request)
    {
        var result = await _userService.CreateUserAsync(request);

        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        var result = await _userService.GetUsersAsync();

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetUser(Guid id)
    {
        var result = await _userService.GetUserByIdAsync(id);

        if (result == null)
            return NotFound();

        return Ok(result);
    }
   
    [HttpPost("{userId}/scope")]
    public async Task<IActionResult> AssignScope(
        Guid userId,
        [FromBody] AssignScopeDto request)
    {
        await _userService.AssignScopeAsync(
            userId,
            request.ConstituencyId,
            request.IslandId);

        return Ok("Scope assigned successfully.");
    }
    /// <summary>
    /// Issues a new authorization code. The response is the only place the
    /// new code ever appears; it is not stored in readable form.
    /// </summary>
    [HttpPost("{userId}/reset-code")]
    public async Task<IActionResult> ResetAuthorizationCode(Guid userId)
    {
        var code = await _userService.ResetAuthorizationCodeAsync(userId);

        if (code == null)
            return NotFound();

        return Ok(new { authorizationCode = code });
    }

    /// <summary>Sets all of a user's roles at once (at least one).</summary>
    [HttpPut("{userId}/roles")]
    public async Task<IActionResult> SetRoles(
        Guid userId,
        [FromBody] SetRolesDto request)
    {
        await _userService.SetRolesAsync(userId, request.RoleIds);

        return NoContent();
    }

    /// <summary>Adds an area: a whole constituency, or one island in it.</summary>
    [HttpPost("{userId}/scopes")]
    public async Task<IActionResult> AddScope(
        Guid userId,
        [FromBody] AssignScopeDto request)
    {
        return Ok(await _userService.AddScopeAsync(
            userId,
            request.ConstituencyId,
            request.IslandId));
    }

    [HttpDelete("{userId}/scopes/{scopeId}")]
    public async Task<IActionResult> RemoveScope(Guid userId, Guid scopeId)
    {
        await _userService.RemoveScopeAsync(userId, scopeId);

        return NoContent();
    }

    [HttpPost("{userId}/role")]
    public async Task<IActionResult> AssignRole(
        Guid userId,
        [FromBody] AssignRoleDto request)
    {
        await _userService.AssignRoleAsync(
            userId,
            request.RoleId);

        return Ok("Role assigned successfully.");
    }

}