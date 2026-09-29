using HR28.Application.DTOs.Voters;

namespace HR28.Application.Interfaces;

public interface IVoterService
{
    Task<VoterDto> CreateVoterAsync(
        CreateVoterDto request);

    Task<List<VoterDto>> GetVotersAsync(Guid userId);


    Task<List<VoterDto>> SearchVotersAsync(
        Guid userId,
        string searchTerm);
    Task UpdateVoterAsync(
    Guid id,
    UpdateVoterDto request);
    Task DeleteVoterAsync(Guid id);

    Task<List<VoterDto>> GetRecentAsync(
    int count = 10);
    Task<VoterDto?> GetVoterByIdAsync(
        Guid userId,
        Guid voterId);

    Task<VoterProfileDto> GetProfileAsync(
        Guid userId,
        Guid voterId);

}