using HR28.Application.Interfaces;
using HR28.Domain.Entities;
using HR28.Infrastructure.Data;

namespace HR28.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly HR28DbContext _context;

    public AuditService(HR28DbContext context)
    {
        _context = context;
    }
    public async Task LogAsync(
        Guid? userId,
        string action,
        string entityName,
        string? entityId)
    {
        var audit = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            CreatedAt = DateTime.UtcNow
        };

        _context.AuditLogs.Add(audit);

        await _context.SaveChangesAsync();
    }


}