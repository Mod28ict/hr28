using System.ComponentModel.DataAnnotations;

namespace HR28.Web.Models;

public class VoterCreateEditDto
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Enter the National ID.")]
    [StringLength(7, ErrorMessage = "National ID is 7 characters, e.g. A123456.")]
    [RegularExpression(@"^[A-Za-z]\d{6}$", ErrorMessage = "National ID must be one letter followed by 6 digits, e.g. A123456.")]
    public string NationalId { get; set; } = "";

    public string FullName { get; set; } = "";

    public string Address { get; set; } = "";

    [StringLength(7, ErrorMessage = "Mobile number is 7 digits.")]
    [RegularExpression(@"^\d{7}$", ErrorMessage = "Mobile number must be exactly 7 digits, e.g. 7771234.")]
    public string? MobileNumber { get; set; } = string.Empty;

    public Guid ConstituencyId { get; set; }

    public Guid? IslandId { get; set; }

    /// <summary>Empty = "Not known" (the default).</summary>
    public Guid? PoliticalPartyId { get; set; }

    /// <summary>"M", "F" or empty.</summary>
    public string? Gender { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Display(Name = "Date of birth")]
    public DateOnly? DateOfBirth { get; set; }

    public string? Remarks { get; set; } = string.Empty;

    public string SupportStatus { get; set; } = "";
}