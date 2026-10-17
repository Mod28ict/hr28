using System.ComponentModel.DataAnnotations;

public class CreateInfluencerDto
{
    [MaxLength(20, ErrorMessage = "National ID can be at most 20 characters.")]
    public string NationalId { get; set; } = string.Empty;

    [MaxLength(150, ErrorMessage = "Full name can be at most 150 characters.")]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(250, ErrorMessage = "Address can be at most 250 characters.")]
    public string Address { get; set; } = string.Empty;

    [MaxLength(20, ErrorMessage = "Contact number can be at most 20 characters.")]
    public string ContactNumber { get; set; } = string.Empty;

    public Guid ConstituencyId { get; set; }

    public Guid? IslandId { get; set; }

    /// <summary>Optional influencer category (MP, Island Council, …).</summary>
    public Guid? CategoryId { get; set; }

    [MaxLength(1000, ErrorMessage = "Remarks can be at most 1000 characters.")]
    public string? Remarks { get; set; } = string.Empty;
}