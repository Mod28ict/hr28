using HR28.Application.DTOs.Pledges;

namespace HR28.Application.Interfaces;

public interface IPledgeService
{
    Task<PledgeDto> CreateAsync(
        Guid userId,
        CreatePledgeDto request);

    Task<List<PledgeDto>>
        GetByVoterIdAsync(Guid voterId);
    Task<PledgeDto> UpdateStatusAsync(
    Guid pledgeId,
    UpdatePledgeStatusDto request);
}