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
}