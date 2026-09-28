using HR28.Application.DTOs.Encounters;

namespace HR28.Application.Interfaces;

public interface IEncounterService
{
    Task<EncounterDto> CreateAsync(
        Guid userId,
        CreateEncounterDto request);

    Task<List<EncounterDto>>
        GetByVoterIdAsync(Guid voterId);
}