namespace HR28.Application.DTOs.Encounters;

public class CreateEncounterDto
{
    public Guid VoterId { get; set; }

    public DateTime EncounterDate { get; set; }
        = HR28.Application.Common.MaldivesTime.Now;

    public string EncounterType { get; set; }
        = string.Empty;

    public string Outcome { get; set; }
        = string.Empty;

    /// <summary>Supports, Undecided or Does not support.</summary>
    public string? Response { get; set; }

    public string Notes { get; set; }
        = string.Empty;
}