using HR28.Application.DTOs.Encounters;

namespace HR28.Application.Interfaces;

public interface IEncounterService
{
    Task<EncounterDto> CreateAsync(
        Guid userId,
        CreateEncounterDto request);

    Task<List<EncounterDto>>
        GetByVoterIdAsync(Guid voterId);

    /// <summary>One encounter, if its voter is in the current user's areas.</summary>
    Task<EncounterDto> GetByIdAsync(Guid id);

    /// <summary>Needs the Encounters.Edit right and the voter in the user's areas. Audited.</summary>
    Task<EncounterDto> UpdateAsync(Guid id, UpdateEncounterDto request);

    /// <summary>Encounters for voters inside the user's areas, newest first, paged.</summary>
    Task<HR28.Application.DTOs.Common.PagedResult<EncounterListItemDto>> GetListAsync(
        int page,
        int pageSize,
        EncounterListFilter filter);
}
