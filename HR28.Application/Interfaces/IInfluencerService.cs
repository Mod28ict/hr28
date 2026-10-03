using HR28.Application.DTOs.Influencers;

namespace HR28.Application.Interfaces;

public interface IInfluencerService
{
    Task<InfluencerDto> CreateAsync(
        CreateInfluencerDto request);

    Task<List<InfluencerDto>> GetAllAsync();

    Task LinkToVoterAsync(
        LinkInfluencerDto request);

    Task<List<VoterInfluencerDto>>
        GetByVoterIdAsync(Guid voterId);
    Task UpdateRelationshipAsync(
        UpdateInfluencerRelationshipDto request);

    /// <summary>One influencer in the caller's areas (404 otherwise).</summary>
    Task<InfluencerDto> GetByIdAsync(Guid id);

    /// <summary>Requires the "Edit influencers" right; same checks as create.</summary>
    Task<InfluencerDto> UpdateAsync(Guid id, CreateInfluencerDto request);

    /// <summary>
    /// Requires the "Delete influencers" right. Permanently removes the influencer
    /// and their links to voters. Returns the deleted influencer's name.
    /// </summary>
    Task<string> DeleteAsync(Guid id);

    /// <summary>
    /// Voters linked to an influencer, limited to the user's areas (others are only counted).
    /// </summary>
    Task<InfluencerVotersDto> GetLinkedVotersAsync(
        Guid influencerId,
        int page,
        int pageSize,
        LinkedVoterFilter filter);
}
