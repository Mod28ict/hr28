using HR28.Application.DTOs.Common;
using HR28.Application.DTOs.Voters;

namespace HR28.Application.Interfaces;

public interface IVoterService
{
    Task<VoterDto> CreateVoterAsync(
        CreateVoterDto request);

    Task<PagedResult<VoterDto>> GetVotersAsync(
        Guid userId,
        int page,
        int pageSize,
        string? searchTerm);

    Task<List<VoterDto>> SearchVotersAsync(
        Guid userId,
        string searchTerm);

    Task UpdateVoterAsync(
        Guid id,
        UpdateVoterDto request);

    Task DeleteVoterAsync(
        Guid id);

    /// <summary>
    /// Whether a National ID is already registered. Details (name, id) are only
    /// returned when that voter is inside the user's scope.
    /// </summary>
    Task<NationalIdCheckDto> CheckNationalIdAsync(
        Guid userId,
        string nationalId,
        Guid? excludeVoterId);

    /// <summary>Most recently added voters within the user's scope.</summary>
    Task<List<VoterDto>> GetRecentAsync(
        Guid userId,
        int count = 10);

    Task<VoterDto?> GetVoterByIdAsync(
        Guid userId,
        Guid voterId);

    Task<VoterProfileDto> GetProfileAsync(
        Guid userId,
        Guid voterId);
}