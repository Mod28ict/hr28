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
                    r.Name
                })
                .OrderBy(r => r.Name)
                .ToListAsync();

        return Ok(roles);
    }
}