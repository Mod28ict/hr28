namespace HR28.Web.Models;

public class InfluencerDto
{
    public Guid Id { get; set; }

    public string NationalId { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string ContactNumber { get; set; } = string.Empty;

    public string Remarks { get; set; } = string.Empty;

    public Guid ConstituencyId { get; set; }

    public Guid? IslandId { get; set; }

    public string ConstituencyName { get; set; } = string.Empty;

    public string? IslandName { get; set; }

    public Guid? CategoryId { get; set; }

    /// <summary>Empty when no category is set.</summary>
    public string CategoryName { get; set; } = string.Empty;

    public int LinkedVoters { get; set; }
}
