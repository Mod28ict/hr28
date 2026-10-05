using HR28.Application.DTOs.Voters;

namespace HR28.Application.Interfaces;

/// <summary>The managed list of political parties (Settings → Lists).</summary>
public interface IPoliticalPartyService
{
    Task<List<PoliticalPartyDto>> GetAllAsync();

    Task<PoliticalPartyDto> CreateAsync(SavePoliticalPartyDto request);

    Task<PoliticalPartyDto> UpdateAsync(Guid id, SavePoliticalPartyDto request);

    /// <summary>Only a party no voter has can be deleted.</summary>
    Task<string> DeleteAsync(Guid id);

    /// <summary>Makes the Voters list open on this party; null = open on all parties.</summary>
    Task SetDefaultFilterAsync(Guid? id);
}
