using HR28.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HR28.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class RolesController : ControllerBase
{
    private readonly HR28DbContext _dbContext;

    public RolesController(
        HR28DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetRoles()
    {
        var roles =
            await _dbContext.Roles
                .Select(r => new
                {
                    r.Id,
                    r.Name,
                    r.Description
                })
                .ToListAsync();

        // Highest authority first, so the assignment screen reads top-down.
        return Ok(roles
            .OrderBy(r => HR28.Application.DTOs.Users.RoleOrder.Rank(r.Name))
            .ThenBy(r => r.Name));
    }
}