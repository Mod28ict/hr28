using HR28.API.Extensions;
using HR28.Application.DTOs.Permissions;
using HR28.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HR28.API.Controllers;

/// <summary>Granting rights to roles and users. Administrator only.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AuthorizationPolicies.SuperAdministrator)]
public class PermissionsController : ControllerBase
{
    private readonly IPermissionService _permissionService;

    public PermissionsController(IPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    /// <summary>Every right, and which roles have it.</summary>
    [HttpGet]
    public async Task<IActionResult> GetMatrix()
    {
        return Ok(await _permissionService.GetMatrixAsync());
    }

    [HttpPut("roles/{roleId:guid}")]
    public async Task<IActionResult> SetRolePermissions(
        Guid roleId,
        [FromBody] SetPermissionsDto request)
    {
        await _permissionService.SetRolePermissionsAsync(roleId, request.Permissions);

        return NoContent();
    }

    /// <summary>Extra rights granted to one user on top of their roles.</summary>
    [HttpGet("users/{userId:guid}")]
    public async Task<IActionResult> GetUserPermissions(Guid userId)
    {
        return Ok(await _permissionService.GetUserPermissionsAsync(userId));
    }

    [HttpPut("users/{userId:guid}")]
    public async Task<IActionResult> SetUserPermissions(
        Guid userId,
        [FromBody] SetPermissionsDto request)
    {
        await _permissionService.SetUserPermissionsAsync(userId, request.Permissions);

        return NoContent();
    }
}
