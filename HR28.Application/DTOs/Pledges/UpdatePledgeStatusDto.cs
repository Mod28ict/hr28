using System.ComponentModel.DataAnnotations;

namespace HR28.Application.DTOs.Pledges;

public class UpdatePledgeStatusDto
{
    [MaxLength(20, ErrorMessage = "Status can be at most 20 characters.")]
    public string Status { get; set; }
        = string.Empty;

    [MaxLength(1000, ErrorMessage = "Notes can be at most 1000 characters.")]
    public string ResolutionNotes { get; set; }
        = string.Empty;
}