using System.ComponentModel.DataAnnotations;

namespace HR28.Application.DTOs.Encounters;

public class UpdateEncounterDto
{
    public DateTime EncounterDate { get; set; }

    [MaxLength(50, ErrorMessage = "Encounter type can be at most 50 characters.")]
    public string EncounterType { get; set; }
        = string.Empty;

    [MaxLength(20, ErrorMessage = "Outcome can be at most 20 characters.")]
    public string Outcome { get; set; }
        = string.Empty;

    /// <summary>Supports, Undecided or Does not support.</summary>
    [MaxLength(20, ErrorMessage = "Response can be at most 20 characters.")]
    public string? Response { get; set; }

    [MaxLength(1000, ErrorMessage = "Notes can be at most 1000 characters.")]
    public string Notes { get; set; }
        = string.Empty;
}
