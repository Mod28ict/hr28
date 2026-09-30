using HR28.Application.DTOs.Users;
using HR28.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HR28.API.Controllers;

[Authorize]
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