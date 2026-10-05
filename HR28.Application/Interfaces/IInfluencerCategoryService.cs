using HR28.Application.DTOs.Influencers;

namespace HR28.Application.Interfaces;

/// <summary>The managed list of influencer categories (Settings → Lists).</summary>
public interface IInfluencerCategoryService
{
    Task<List<InfluencerCategoryDto>> GetAllAsync();

    Task<InfluencerCategoryDto> CreateAsync(SaveInfluencerCategoryDto request);

    Task<InfluencerCategoryDto> UpdateAsync(Guid id, SaveInfluencerCategoryDto request);

    /// <summary>Only a category no influencer uses can be deleted.</summary>
    Task<string> DeleteAsync(Guid id);
}
