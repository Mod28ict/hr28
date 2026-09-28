using HR28.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HR28.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AuditLogsController : ControllerBase
{
    private readonly HR28DbContext _context;

    public AuditLogsController(HR28DbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetRecent(int count = 100)
    {
        var logs = await _context.AuditLogs
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .Take(count)
            .ToListAsync();

        return Ok(logs);
    }

    [HttpGet("entity/{entityName}/{entityId}")]
    public async Task<IActionResult> GetByEntity(
        string entityName,
        string entityId)
    {
        var logs = await _context.AuditLogs
            .AsNoTracking()
            .Where(a => a.EntityName == entityName
                     && a.EntityId == entityId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        return Ok(logs);
    }

    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetByUser(Guid userId)
    {
        var logs = await _context.AuditLogs
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        return Ok(logs);
    }
}