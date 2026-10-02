using HR28.Application.DTOs.Audit;
using HR28.Application.DTOs.Common;

namespace HR28.Application.Interfaces;

/// <summary>
/// Reads the audit trail. Administrators see every entry;
/// everyone else sees only the actions they performed.
/// </summary>
public interface IAuditTrailService
{
    Task<PagedResult<AuditEntryDto>> GetPagedAsync(Guid userId, AuditQueryDto query);

    Task<List<AuditEntryDto>> GetRecentAsync(Guid userId, int count);

    Task<AuditFacetsDto> GetFacetsAsync(Guid userId);
}
