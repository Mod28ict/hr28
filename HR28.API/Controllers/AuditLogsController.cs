using HR28.API.Extensions;
using HR28.Application.DTOs.Audit;
using HR28.Application.Interfaces;
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
    private readonly IAuditTrailService _auditTrailService;

    public AuditLogsController(
        HR28DbContext context,
        IAuditTrailService auditTrailService)
    {
        _context = context;
        _auditTrailService = auditTrailService;
    }

    /// <summary>
    /// Paged audit trail. Administrators see every entry; other users see their own actions.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] AuditQueryDto query)
    {
        var userId = User.GetUserId();

        if (userId == null)
            return Unauthorized();

        return Ok(await _auditTrailService.GetPagedAsync(userId.Value, query));
    }

    [HttpGet("facets")]
    public async Task<IActionResult> GetFacets()
    {
        var userId = User.GetUserId();

        if (userId == null)
            return Unauthorized();

        return Ok(await _auditTrailService.GetFacetsAsync(userId.Value));
    }

    [HttpGet("entity/{entityName}/{entityId}")]
    [Authorize(Policy = AuthorizationPolicies.Administrator)]
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
    [Authorize(Policy = AuthorizationPolicies.Administrator)]
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
