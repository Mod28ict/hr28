using HR28.Application.DTOs.Voters;

namespace HR28.Application.Interfaces;

public interface IVoterService
{
    Task<VoterDto> CreateVoterAsync(
        CreateVoterDto request);

    Task<List<VoterDto>> GetVotersAsync();

    Task<VoterDto?> GetVoterByIdAsync(
        Guid id);
    Task<List<VoterDto>> SearchVotersAsync(
    string searchTerm);
}