public class CreateInfluencerDto
{
    public string NationalId { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string ContactNumber { get; set; } = string.Empty;

    public Guid ConstituencyId { get; set; }

    public Guid? IslandId { get; set; }

    /// <summary>Optional influencer category (MP, Island Council, …).</summary>
    public Guid? CategoryId { get; set; }

    public string Remarks { get; set; } = string.Empty;
}