using HR28.Application.DTOs.Pledges;

namespace HR28.Application.Interfaces;

public interface IPledgeService
{
    Task<PledgeDto> CreateAsync(
        Guid userId,
        CreatePledgeDto request);

    Task<List<PledgeDto>>
        GetByVoterIdAsync(Guid voterId);

    /// <summary>Status must be Open, In Progress, Completed or Cancelled.</summary>
    Task<PledgeDto> UpdateStatusAsync(
        Guid pledgeId,
        UpdatePledgeStatusDto request);

    /// <summary>Permanently deletes a pledge whose voter is in the caller's areas. Audited.</summary>
    Task DeleteAsync(Guid pledgeId);

    /// <summary>One pledge whose voter is in the caller's areas.</summary>
    Task<PledgeDto> GetByIdAsync(Guid pledgeId);

    /// <summary>Pledges for voters inside the user's areas, newest first, paged.</summary>
    Task<HR28.Application.DTOs.Common.PagedResult<PledgeListItemDto>> GetListAsync(
        int page,
        int pageSize,
        PledgeListFilter filter);
}
