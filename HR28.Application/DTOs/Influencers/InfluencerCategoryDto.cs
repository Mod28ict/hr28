namespace HR28.Application.DTOs.Influencers;

public class InfluencerCategoryDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    /// <summary>How many influencers use this category (a used category can't be deleted).</summary>
    public int InfluencerCount { get; set; }
}

public class SaveInfluencerCategoryDto
{
    public string Name { get; set; } = string.Empty;
}
