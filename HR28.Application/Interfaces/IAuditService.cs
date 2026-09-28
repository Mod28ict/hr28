namespace HR28.Application.Interfaces;

public interface IAuditService
{
    Task LogAsync(
        Guid? userId,
        string action,
        string entityName,
        string? entityId);
}