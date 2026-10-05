namespace HR28.Domain.Entities;

/// <summary>
/// A kind of influencer, e.g. MP, Island Council, GM Member.
/// Administrators manage the list in Settings → Lists.
/// </summary>
public class InfluencerCategory
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }
}
