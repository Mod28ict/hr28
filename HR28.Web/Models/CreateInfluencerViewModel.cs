using System.ComponentModel.DataAnnotations;

namespace HR28.Web.Models;

public class CreateInfluencerDto
{
    [Required(ErrorMessage = "National ID is required.")]
    [StringLength(20, ErrorMessage = "National ID must be 20 characters or fewer.")]
    [Display(Name = "National ID")]
    public string NationalId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(150)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [StringLength(300)]
    public string Address { get; set; } = string.Empty;

    [StringLength(20)]
    [Display(Name = "Contact number")]
    public string ContactNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please choose a constituency.")]
    [Display(Name = "Constituency")]
    public Guid? ConstituencyId { get; set; }

    [Display(Name = "Island")]
    public Guid? IslandId { get; set; }

    [StringLength(500)]
    public string Remarks { get; set; } = string.Empty;
}

public class CreateInfluencerViewModel
{
    /// <summary>Set when editing an existing influencer; null when adding.</summary>
    public Guid? Id { get; set; }

    public bool IsEdit => Id.HasValue;

    public CreateInfluencerDto Influencer { get; set; } = new();

    public List<LookupDto> Constituencies { get; set; } = new();
}
