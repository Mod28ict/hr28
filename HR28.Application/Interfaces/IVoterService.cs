using HR28.Application.DTOs.Voters;

namespace HR28.Application.Interfaces;

public interface IVoterService
{
    Task<VoterDto> CreateVoterAsync(
        CreateVoterDto request);

    Task<List<VoterDto>> GetVotersAsync(Guid userId);

    Task<VoterDto?> GetVoterByIdAsync(
        Guid id);
    Task<List<VoterDto>> SearchVotersAsync(
    string searchTerm);
    Task UpdateVoterAsync(
    Guid id,
    UpdateVoterDto request);
    Task DeleteVoterAsync(Guid id);
    Task<VoterProfileDto> GetProfileAsync(
        Guid voterId);
    Task<List<VoterDto>> GetRecentAsync(
    int count = 10);

}