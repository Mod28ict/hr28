using System.ComponentModel.DataAnnotations;

namespace HR28.Application.DTOs.Voters;

public class CreateVoterDto
{
    [MaxLength(20, ErrorMessage = "National ID can be at most 20 characters.")]
    public string NationalId { get; set; } = string.Empty;

    [MaxLength(150, ErrorMessage = "Full name can be at most 150 characters.")]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(250, ErrorMessage = "Address can be at most 250 characters.")]
    public string Address { get; set; } = string.Empty;

    [MaxLength(20, ErrorMessage = "Mobile number can be at most 20 characters.")]
    public string MobileNumber { get; set; } = string.Empty;

    public Guid ConstituencyId { get; set; }

    public Guid? IslandId { get; set; }

    /// <summary>Empty = "Not known".</summary>
    public Guid? PoliticalPartyId { get; set; }

    /// <summary>"M", "F" or empty. On update, null keeps the current value.</summary>
    [MaxLength(10, ErrorMessage = "Gender can be at most 10 characters.")]
    public string? Gender { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    [MaxLength(1000, ErrorMessage = "Remarks can be at most 1000 characters.")]
    public string? Remarks { get; set; } = string.Empty;
    [MaxLength(20, ErrorMessage = "Status can be at most 20 characters.")]
    public string SupportStatus { get; set; }
    = "Undecided";

}